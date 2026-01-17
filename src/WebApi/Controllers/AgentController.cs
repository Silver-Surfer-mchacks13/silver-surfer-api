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
    /// Create a new conversation session and get initial actions
    /// </summary>
    /// <param name="request">The request containing user goal and initial page state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response with session ID and initial actions</returns>
    /// <response code="200">Returns the agent response with session ID and actions</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(AgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentResponse>> CreateConversation(
        [FromBody] CreateConversationRequest request,
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
            "Creating new conversation. Goal: {Goal}, Url: {Url}",
            request.UserGoal,
            request.PageState.Url);

        try
        {
            var response = await _agentService.CreateConversationAsync(request, cancellationToken);

            _logger.LogInformation(
                "Conversation created. SessionId: {SessionId}, Actions: {ActionCount}, Complete: {Complete}",
                response.SessionId,
                response.Actions.Count,
                response.Complete);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversation");
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Message = "Failed to create conversation",
                ErrorCode = "CONVERSATION_CREATE_FAILED"
            });
        }
    }

    /// <summary>
    /// Continue an existing conversation and get next actions
    /// </summary>
    /// <param name="sessionId">The conversation session ID</param>
    /// <param name="request">The request containing current page state (URL and HTML)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent response with next actions</returns>
    /// <response code="200">Returns the agent response with actions</response>
    /// <response code="400">Invalid request data or session is completed/failed</response>
    /// <response code="404">Session not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("conversations/{sessionId}/continue")]
    [ProducesResponseType(typeof(AgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentResponse>> ContinueConversation(
        [FromRoute] Guid sessionId,
        [FromBody] ContinueConversationRequest request,
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
            "Continuing conversation. SessionId: {SessionId}, Url: {Url}",
            sessionId,
            request.PageState.Url);

        try
        {
            var response = await _agentService.ContinueConversationAsync(sessionId, request, cancellationToken);

            _logger.LogInformation(
                "Conversation continued. SessionId: {SessionId}, Actions: {ActionCount}, Complete: {Complete}",
                response.SessionId,
                response.Actions.Count,
                response.Complete);

            return Ok(response);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Conversation session not found: {SessionId}", sessionId);
            return NotFound(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "SESSION_NOT_FOUND"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for session {SessionId}: {Message}", sessionId, ex.Message);
            return BadRequest(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "INVALID_SESSION_STATE"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation");
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Message = "Failed to continue conversation",
                ErrorCode = "CONVERSATION_CONTINUE_FAILED"
            });
        }
    }
}
