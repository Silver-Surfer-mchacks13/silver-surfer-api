using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Web.Common.DTOs.Agent;
using WebApi.Configuration.Options;
using WebApi.Data;
using WebApi.Exceptions;
using WebApi.Models;

namespace WebApi.Services.Agent;

/// <summary>
/// Service that orchestrates the AI agent using Semantic Kernel
/// </summary>
public class AgentService
{
    private readonly Kernel _kernel;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AgentService> _logger;
    private readonly GeminiOptions _geminiOptions;

    public AgentService(
        Kernel kernel,
        AppDbContext dbContext,
        ILogger<AgentService> logger,
        IOptions<GeminiOptions> geminiOptions)
    {
        _kernel = kernel;
        _dbContext = dbContext;
        _logger = logger;
        _geminiOptions = geminiOptions.Value;
    }

    /// <summary>
    /// Creates a new conversation session and returns initial actions
    /// </summary>
    public async Task<AgentResponse> CreateConversationAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Create new task session
        var session = new TaskSession
        {
            Id = Guid.NewGuid(),
            UserId = null, // Will be set when authorization is added
            Goal = request.UserGoal,
            Status = TaskSessionStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.TaskSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new conversation session {SessionId} with goal: {Goal}", 
            session.Id, request.UserGoal);

        // Process the initial conversation request
        var continueRequest = new ContinueConversationRequest
        {
            PageState = request.PageState
        };

        return await ContinueConversationAsync(session.Id, continueRequest, cancellationToken);
    }

    /// <summary>
    /// Continues an existing conversation and returns next actions
    /// </summary>
    public async Task<AgentResponse> ContinueConversationAsync(
        Guid sessionId,
        ContinueConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Load existing session
        var session = await _dbContext.TaskSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new NotFoundException($"Conversation session {sessionId} not found");
        }

        // Validate session is still active
        if (session.Status == TaskSessionStatus.Completed)
        {
            throw new InvalidOperationException($"Conversation session {sessionId} is already completed");
        }

        if (session.Status == TaskSessionStatus.Failed || session.Status == TaskSessionStatus.Cancelled)
        {
            throw new InvalidOperationException($"Conversation session {sessionId} is in {session.Status} status and cannot be continued");
        }

        try
        {
            // Process the conversation
            return await ProcessConversationAsync(session, request.PageState, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing conversation for session {SessionId}", sessionId);
            
            session.Status = TaskSessionStatus.Failed;
            session.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Shared logic for processing a conversation request
    /// </summary>
    private async Task<AgentResponse> ProcessConversationAsync(
        TaskSession session,
        PageState pageState,
        CancellationToken cancellationToken)
    {
        // Build system prompt with context
        var systemPrompt = BuildSystemPrompt(session.Goal, session.Id, pageState.Url);

        // Get chat completion service
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // Create chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);

        // Add conversation history from previous actions
        var previousActions = await _dbContext.AgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderBy(a => a.CreatedAt)
            .Take(10) // Last 10 actions for context
            .ToListAsync(cancellationToken);

        if (previousActions.Any())
        {
            var historyContext = string.Join("\n", previousActions.Select(a => 
                $"- {a.ActionType} on {a.Target ?? "page"}: {a.Reasoning}"));
            chatHistory.AddUserMessage($"Previous actions:\n{historyContext}");
        }

        // Note: Previous action results are inferred from page state changes
        // The AI can see what changed by comparing the current page state with previous actions

        // Add current page state
        var pageContext = $"Current page URL: {pageState.Url}\n" +
                        $"HTML content (truncated): {pageState.Html.Substring(0, Math.Min(5000, pageState.Html.Length))}";
        chatHistory.AddUserMessage($"Current page state:\n{pageContext}");

        // Add user goal (only if this is the first action)
        if (!previousActions.Any())
        {
            chatHistory.AddUserMessage($"User goal: {session.Goal}");
        }

        // Add BrowserPlugin to kernel
        var browserPlugin = new BrowserPlugin();
        _kernel.Plugins.AddFromObject(browserPlugin, "BrowserPlugin");

        // Invoke the kernel to get agent response
        var result = await chatCompletionService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings: null,
            kernel: _kernel,
            cancellationToken: cancellationToken);

        // Parse the response to extract actions
        var responseMessage = result.LastOrDefault();
        if (responseMessage == null)
        {
            throw new InvalidOperationException("No response from AI agent");
        }

        var actions = ParseActionsFromResponse(responseMessage.Content ?? "", pageState);
        var reasoning = ExtractReasoning(responseMessage.Content ?? "");

        // Save actions to database
        foreach (var action in actions)
        {
            var agentAction = new AgentAction
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                ActionType = action.Action,
                Target = action.Target,
                Value = action.Value,
                Reasoning = reasoning,
                Success = false, // Will be updated by extension after execution
                PageUrl = pageState.Url,
                PageHtml = TruncateHtml(pageState.Html),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AgentActions.Add(agentAction);
        }

        // Update session status
        var isComplete = actions.Any(a => a.Action == "complete");
        if (isComplete)
        {
            session.Status = TaskSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AgentResponse
        {
            SessionId = session.Id,
            Actions = actions,
            Reasoning = reasoning,
            Complete = isComplete,
            NeedsUserInput = null // Could be extracted from response if needed
        };
    }

    private string BuildSystemPrompt(string userGoal, Guid sessionId, string currentUrl)
    {
        return $@"You are an AI assistant helping elderly users navigate websites step-by-step.

Your goal: {userGoal}
Current page: {currentUrl}
Session ID: {sessionId}

You have access to the following browser functions:
- ClickElement(selector, reasoning): Click an element on the page
- TypeText(selector, text, reasoning): Type text into an input field
- Navigate(url, reasoning): Navigate to a different URL
- Wait(seconds, reasoning): Wait for a specified number of seconds
- Scroll(direction, reasoning): Scroll the page up or down
- Complete(message): Mark the task as complete

Analyze the HTML content provided and determine the next action(s) needed to accomplish the user's goal.
Use the browser functions to interact with the page. Return your actions clearly and explain your reasoning.
If the task is complete, call the Complete function with a summary message.";
    }

    private List<BrowserAction> ParseActionsFromResponse(string responseContent, PageState pageState)
    {
        var actions = new List<BrowserAction>();

        // Try to parse function calls from the response
        // This is a simplified parser - in production, you'd want more robust parsing
        // Semantic Kernel should handle function calling automatically, but we need to extract the results

        // For now, we'll look for patterns in the response text
        // In a real implementation, Semantic Kernel's function calling would handle this automatically
        // and we'd extract the function calls from the ChatMessageContent

        // Simple heuristic: if response mentions specific actions, create them
        // This is a placeholder - actual implementation should use SK's function calling results
        if (responseContent.Contains("click", StringComparison.OrdinalIgnoreCase) ||
            responseContent.Contains("ClickElement", StringComparison.OrdinalIgnoreCase))
        {
            // Extract selector from response (simplified)
            var selectorMatch = System.Text.RegularExpressions.Regex.Match(
                responseContent,
                @"(?:click|ClickElement)[\s(]+['""]?([^'"")\s]+)['""]?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (selectorMatch.Success)
            {
                actions.Add(new BrowserAction
                {
                    Action = "click",
                    Target = selectorMatch.Groups[1].Value,
                    Reasoning = responseContent.Substring(0, Math.Min(200, responseContent.Length))
                });
            }
        }

        // If no actions found, return a default wait action to prevent infinite loops
        if (!actions.Any())
        {
            actions.Add(new BrowserAction
            {
                Action = "wait",
                Duration = 1,
                Reasoning = "Analyzing page content..."
            });
        }

        return actions;
    }

    private string ExtractReasoning(string responseContent)
    {
        // Extract reasoning from response (max 5000 chars to match database limit)
        if (string.IsNullOrEmpty(responseContent))
            return string.Empty;
            
        const int maxLength = 5000;
        return responseContent.Length <= maxLength 
            ? responseContent 
            : responseContent.Substring(0, maxLength);
    }

    private string TruncateHtml(string html)
    {
        // Truncate HTML to 50000 chars for storage (including truncation marker)
        const int maxLength = 50000;
        const string truncationMarker = "... [truncated]";
        
        if (html == null)
            return string.Empty;
            
        if (html.Length <= maxLength)
            return html;

        // Ensure total length doesn't exceed maxLength
        return html.Substring(0, maxLength - truncationMarker.Length) + truncationMarker;
    }
}
