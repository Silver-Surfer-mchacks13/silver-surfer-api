using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Web.Common.DTOs;
using Web.Common.DTOs.Agent;
using WebApi.Exceptions;
using WebApi.Services.Agent;

namespace WebApi.Controllers;

/// <summary>
/// Manages AI agent requests for browser automation
/// </summary>
/// <remarks>
/// TODO: Add [Authorize] attribute when authorization is implemented
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("Global")]
public class AgentController : ControllerBase
{
    private readonly AgentService _agentService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(AgentService agentService, ILogger<AgentController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Process a conversation request - creates new conversation if SessionId is omitted, otherwise continues existing conversation
    /// </summary>
    /// <param name="request">The request containing page state and optional session ID. The session_id field is optional: omit it to create a new conversation, or provide it to continue an existing conversation.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response with session ID and actions</returns>
    /// <remarks>
    /// Request: The session_id field in the request body is optional. When omitted, a new conversation session is created. When provided, the request continues the existing conversation.
    /// 
    /// Response Actions Schema: The response contains an array of actions. Each action has an "action_type" discriminator field that determines the action type. The possible action types are:
    /// 
    /// • click: Click an element on the page using an XPath expression.
    /// 
    ///   <code>
    ///   {
    ///     "action_type": "click",
    ///     "timestamp": "datetime (UTC, ISO 8601 format)",
    ///     "x_path": "string (XPath expression, required, 1-500 chars)",
    ///     "reasoning": "string (optional)"
    ///   }
    ///   </code>
    /// 
    ///   XPath examples: '//button[@id=""submit""]', '//a[@href=""/login""]', '//input[@type=""text"" and @name=""email""]'
    /// 
    /// • wait: Wait for a specified number of seconds.
    /// 
    ///   <code>
    ///   {
    ///     "action_type": "wait",
    ///     "timestamp": "datetime (UTC, ISO 8601 format)",
    ///     "duration": "integer (required, 0-300 seconds)",
    ///     "reasoning": "string (optional)"
    ///   }
    ///   </code>
    /// 
    /// • message: Display a message to the user (non-terminal - frontend continues sending requests).
    /// 
    ///   <code>
    ///   {
    ///     "action_type": "message",
    ///     "timestamp": "datetime (UTC, ISO 8601 format)",
    ///     "message": "string (required, 1-1000 chars)",
    ///     "reasoning": "string (optional)"
    ///   }
    ///   </code>
    /// 
    /// • complete: Mark the task as complete (terminal - frontend stops sending requests).
    /// 
    ///   <code>
    ///   {
    ///     "action_type": "complete",
    ///     "timestamp": "datetime (UTC, ISO 8601 format)",
    ///     "message": "string (required, 1-1000 chars)",
    ///     "reasoning": "string (optional)"
    ///   }
    ///   </code>
    /// </remarks>
    /// <response code="200">Returns the agent response with session ID and actions</response>
    /// <response code="400">Invalid request data or session is completed</response>
    /// <response code="404">Session not found (when SessionId is provided)</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(AgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentResponse>> ProcessConversation(
        [FromBody] ConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid request data",
                Errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>())
            });
        }

        _logger.LogInformation(
            "Processing conversation request. SessionId: {SessionId}, Title: {Title}, Url: {Url}",
            request.SessionId,
            request.Title,
            request.PageState.Url);

        try
        {
            var response = await _agentService.ProcessConversationRequestAsync(request, cancellationToken);

            _logger.LogInformation(
                "Conversation processed. SessionId: {SessionId}, Actions: {ActionCount}, Complete: {Complete}",
                response.SessionId,
                response.Actions.Count,
                response.Complete);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid conversation request: {Message}", ex.Message);
            return BadRequest(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "INVALID_REQUEST"
            });
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Conversation session not found: {Message}", ex.Message);
            return NotFound(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "SESSION_NOT_FOUND"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            return BadRequest(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "INVALID_SESSION_STATE"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing conversation");
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Message = "Failed to process conversation",
                ErrorCode = "CONVERSATION_PROCESSING_FAILED"
            });
        }
    }

    /// <summary>
    /// Get all actions for a conversation session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all actions for the session, sorted chronologically</returns>
    /// <response code="200">Returns all actions for the session</response>
    /// <response code="404">Session not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("conversations/{sessionId:guid}/actions")]
    [ProducesResponseType(typeof(List<BrowserAction>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BrowserAction>>> GetConversationActions(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify session exists
            var sessionExists = await _agentService.SessionExistsAsync(sessionId, cancellationToken);
            if (!sessionExists)
            {
                return NotFound(new ErrorResponse
                {
                    Message = $"Conversation session {sessionId} not found",
                    ErrorCode = "SESSION_NOT_FOUND"
                });
            }

            var actions = await _agentService.GetAllActionsAsync(sessionId, cancellationToken);

            _logger.LogInformation(
                "Retrieved {ActionCount} actions for session {SessionId}",
                actions.Count,
                sessionId);

            return Ok(actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving actions for session {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Message = "Failed to retrieve conversation actions",
                ErrorCode = "ACTION_RETRIEVAL_FAILED"
            });
        }
    }
}
