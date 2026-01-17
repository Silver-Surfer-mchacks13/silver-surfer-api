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
    /// Processes a conversation request - creates new session if SessionId is null, otherwise continues existing session
    /// </summary>
    public async Task<AgentResponse> ProcessConversationRequestAsync(
        ConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        TaskSession session;

        if (request.SessionId.HasValue)
        {
            // Continue existing conversation
            session = await _dbContext.TaskSessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId.Value, cancellationToken);

            if (session == null)
            {
                throw new NotFoundException($"Conversation session {request.SessionId.Value} not found");
            }

            // Validate session is still active
            if (session.CompletedAt.HasValue)
            {
                throw new InvalidOperationException($"Conversation session {request.SessionId.Value} is already completed");
            }

            _logger.LogInformation("Continuing conversation session {SessionId}", session.Id);
        }
        else
        {
            // Create new conversation
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException("Title is required when creating a new conversation", nameof(request));
            }

            session = new TaskSession
            {
                Id = Guid.NewGuid(),
                UserId = null, // Will be set when authorization is added
                Title = request.Title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.TaskSessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new conversation session {SessionId} with title: {Title}", 
                session.Id, request.Title);
        }

        // Process the conversation
        return await ProcessConversationAsync(session, request.PageState, cancellationToken);
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
        var systemPrompt = BuildSystemPrompt(session.Title, session.Id, pageState.Url);

        // Get chat completion service
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // Create chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);

        // Add conversation history from previous actions (load from all action tables)
        var clickActions = await _dbContext.ClickAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "click", Description = $"click on {a.Target}", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        var waitActions = await _dbContext.WaitAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "wait", Description = $"wait {a.Duration}s", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        var completeActions = await _dbContext.CompleteAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "complete", Description = $"complete: {a.Message}", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        var allPreviousActions = clickActions
            .Concat(waitActions)
            .Concat(completeActions)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .ToList();

        if (allPreviousActions.Any())
        {
            var historyContext = string.Join("\n", allPreviousActions.Select(a => 
                $"- {a.Type} ({a.Description}): {a.Reasoning}"));
            chatHistory.AddUserMessage($"Previous actions:\n{historyContext}");
        }

        // Note: Previous action results are inferred from page state changes
        // The AI can see what changed by comparing the current page state with previous actions

        // Add current page state
        var pageContext = $"Current page URL: {pageState.Url}\n" +
                        $"HTML content (truncated): {pageState.Html.Substring(0, Math.Min(5000, pageState.Html.Length))}";
        chatHistory.AddUserMessage($"Current page state:\n{pageContext}");

        // Add user goal (only if this is the first action)
        if (!allPreviousActions.Any())
        {
            chatHistory.AddUserMessage($"User goal: {session.Title}");
        }

        // Add BrowserPlugin to kernel
        var browserPlugin = new BrowserPlugin();
        _kernel.Plugins.AddFromObject(browserPlugin, "BrowserPlugin");

        // Invoke the kernel to get agent response with function calling enabled
        var result = await chatCompletionService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings: null,
            kernel: _kernel,
            cancellationToken: cancellationToken);

        // Extract actions from function calls or response text
        var responseMessage = result.LastOrDefault();
        if (responseMessage == null)
        {
            throw new InvalidOperationException("No response from AI agent");
        }

        // Try to extract function calls from Semantic Kernel's response
        var actions = ExtractActionsFromFunctionCalls(responseMessage, pageState);

        // Save actions to database using the appropriate model for each action type
        foreach (var action in actions)
        {
            switch (action)
            {
                case ClickAction clickAction:
                    var clickDbAction = new ClickAgentAction
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Target = clickAction.XPath, // Stored as Target in DB but represents XPath
                        Reasoning = action.Reasoning ?? string.Empty,
                        Success = false,
                        PageUrl = pageState.Url,
                        PageHtml = TruncateHtml(pageState.Html),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.ClickAgentActions.Add(clickDbAction);
                    break;

                case MessageAction messageAction:
                    // MessageAction is informational only - no need to save to database
                    // It's just for displaying messages to the user
                    break;

                case WaitAction waitAction:
                    var waitDbAction = new WaitAgentAction
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Duration = waitAction.Duration,
                        Reasoning = action.Reasoning ?? string.Empty,
                        Success = false,
                        PageUrl = pageState.Url,
                        PageHtml = TruncateHtml(pageState.Html),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.WaitAgentActions.Add(waitDbAction);
                    break;

                case CompleteAction completeAction:
                    var completeDbAction = new CompleteAgentAction
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Message = completeAction.Message,
                        Reasoning = action.Reasoning ?? string.Empty,
                        Success = false,
                        PageUrl = pageState.Url,
                        PageHtml = TruncateHtml(pageState.Html),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.CompleteAgentActions.Add(completeDbAction);
                    break;
            }
        }

        // Update session completion
        var isComplete = actions.Any(a => a is CompleteAction);
        if (isComplete)
        {
            session.CompletedAt = DateTime.UtcNow;
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AgentResponse
        {
            SessionId = session.Id,
            Actions = actions,
            Complete = isComplete
        };
    }

    private string BuildSystemPrompt(string userGoal, Guid sessionId, string currentUrl)
    {
        return $@"You are an AI assistant helping elderly users navigate websites step-by-step.

Your goal: {userGoal}
Current page: {currentUrl}
Session ID: {sessionId}

You have access to the following browser functions:
- ClickElement(xpath, reasoning): Click an element on the page using an XPath expression (e.g., '//button[@id=""login""]', '//a[@href=""/login""]')
- Wait(seconds, reasoning): Wait for a specified number of seconds
- Message(message): Display a message to the user (non-terminal - frontend continues sending requests)
- Complete(message): Mark the task as complete (terminal - frontend stops sending requests)

IMPORTANT: Use XPath expressions, NOT CSS selectors, for ClickElement. XPath examples:
- '//button[@id=""submit""]' - button with id=""submit""
- '//a[@href=""/login""]' - link with href=""/login""
- '//input[@type=""text"" and @name=""email""]' - input with type=""text"" and name=""email""
- '//div[@class=""button"" and contains(text(), ""Click me"")]' - div with class=""button"" containing text ""Click me""

Analyze the HTML content provided and determine the next action(s) needed to accomplish the user's goal.
Use the browser functions to interact with the page. Return your actions clearly and explain your reasoning.
Use Message() for informational messages that don't stop the conversation. Use Complete() only when the task is fully finished.";
    }

    /// <summary>
    /// Extracts browser actions from Semantic Kernel's response
    /// Attempts to extract from function calls first, then falls back to text parsing
    /// </summary>
    private List<BrowserAction> ExtractActionsFromFunctionCalls(ChatMessageContent responseMessage, PageState pageState)
    {
        var actions = new List<BrowserAction>();

        // Try to extract from Items collection if available (Semantic Kernel function calls)
        // Note: Vertex AI may not return structured function calls, so we check Items first
        if (responseMessage.Items != null && responseMessage.Items.Count > 0)
        {
            // Check metadata or items for function call information
            // In SK, function calls might be represented differently depending on the provider
            foreach (var item in responseMessage.Items)
            {
                // Try to extract function call info from item metadata or type
                // This is provider-dependent, so we check generically
                var itemType = item.GetType().Name;
                if (itemType.Contains("Function") || itemType.Contains("Call"))
                {
                    // Attempt to extract function name and arguments via reflection or metadata
                    // For now, we'll fall through to text parsing since Vertex AI structure is unknown
                }
            }
        }

        // Primary method: Parse from text content
        // This works reliably with Vertex AI responses and handles both function call formats and natural language
        actions = ParseActionsFromText(responseMessage.Content ?? "", pageState);

        return actions;
    }

    /// <summary>
    /// Fallback: Parse actions from text response (used when function calling isn't available)
    /// </summary>
    private List<BrowserAction> ParseActionsFromText(string responseContent, PageState pageState)
    {
        var actions = new List<BrowserAction>();

        // Parse Complete action (terminal - return immediately if found)
        // Only match function call syntax: Complete(...) or complete(...), not just the word "complete" in text
        // Use word boundary to avoid matching "complete" in phrases like "complete checkout"
        var completeMatch = System.Text.RegularExpressions.Regex.Match(
            responseContent,
            @"\b(?:Complete|complete)\s*\(\s*['""]?([^'""\)]+)['""]?\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (completeMatch.Success)
        {
            actions.Add(new CompleteAction
            {
                Message = completeMatch.Groups[1].Value.Trim(),
                Reasoning = responseContent.Substring(0, Math.Min(200, responseContent.Length))
            });
            return actions; // Complete is terminal
        }

        // Parse Click action - look for ClickElement(...) function call
        // Handle formats: ClickElement('//xpath', 'reasoning') or ClickElement(xpath='//xpath', reasoning='...')
        var clickMatch = System.Text.RegularExpressions.Regex.Match(
            responseContent,
            @"\b(?:ClickElement|click)\s*\(\s*(?:(?:xpath|selector)\s*=\s*)?['""]([^'""]+)['""]\s*(?:,\s*(?:reasoning\s*=\s*)?['""]([^'""]+)['""])?\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (clickMatch.Success)
        {
            actions.Add(new ClickAction
            {
                XPath = clickMatch.Groups[1].Value,
                Reasoning = clickMatch.Groups.Count > 2 && !string.IsNullOrEmpty(clickMatch.Groups[2].Value)
                    ? clickMatch.Groups[2].Value
                    : responseContent.Substring(0, Math.Min(200, responseContent.Length))
            });
        }

        // Parse Message action - look for Message(...) function call
        var messageMatch = System.Text.RegularExpressions.Regex.Match(
            responseContent,
            @"\b(?:Message|message)\s*\(\s*['""]?([^'""\)]+)['""]?\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (messageMatch.Success)
        {
            actions.Add(new MessageAction
            {
                Message = messageMatch.Groups[1].Value.Trim(),
                Reasoning = responseContent.Substring(0, Math.Min(200, responseContent.Length))
            });
        }

        // Parse Wait action - look for Wait(...) function call
        var waitMatch = System.Text.RegularExpressions.Regex.Match(
            responseContent,
            @"\b(?:Wait|wait)\s*\(\s*(\d+)\s*(?:,\s*['""]([^'""]+)['""])?\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (waitMatch.Success && int.TryParse(waitMatch.Groups[1].Value, out var seconds))
        {
            actions.Add(new WaitAction
            {
                Duration = seconds,
                Reasoning = waitMatch.Groups.Count > 2 && !string.IsNullOrEmpty(waitMatch.Groups[2].Value)
                    ? waitMatch.Groups[2].Value
                    : "Waiting as requested"
            });
        }

        // If no actions found, return a default wait action to prevent infinite loops
        if (!actions.Any())
        {
            actions.Add(new WaitAction
            {
                Duration = 1,
                Reasoning = "Analyzing page content..."
            });
        }

        return actions;
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
