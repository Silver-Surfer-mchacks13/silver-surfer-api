using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    /// Checks if a session exists
    /// </summary>
    public async Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TaskSessions
            .AnyAsync(s => s.Id == sessionId, cancellationToken);
    }

    /// <summary>
    /// Gets all actions for a conversation session
    /// </summary>
    public async Task<List<BrowserAction>> GetAllActionsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actions = new List<BrowserAction>();

        // Fetch click actions
        var clickActions = await _dbContext.ClickAgentActions
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var dbAction in clickActions)
        {
            actions.Add(new ClickAction
            {
                XPath = dbAction.Target, // Target stores XPath expression
                Reasoning = dbAction.Reasoning,
                Timestamp = dbAction.CreatedAt
            });
        }

        // Fetch wait actions
        var waitActions = await _dbContext.WaitAgentActions
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var dbAction in waitActions)
        {
            actions.Add(new WaitAction
            {
                Duration = dbAction.Duration,
                Reasoning = dbAction.Reasoning,
                Timestamp = dbAction.CreatedAt
            });
        }

        // Fetch complete actions
        var completeActions = await _dbContext.CompleteAgentActions
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var dbAction in completeActions)
        {
            actions.Add(new CompleteAction
            {
                Message = dbAction.Message,
                Reasoning = dbAction.Reasoning,
                Timestamp = dbAction.CreatedAt
            });
        }

        // Fetch message actions
        var messageActions = await _dbContext.MessageAgentActions
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var dbAction in messageActions)
        {
            actions.Add(new MessageAction
            {
                Message = dbAction.Message,
                Reasoning = dbAction.Reasoning,
                Timestamp = dbAction.CreatedAt
            });
        }

        // Sort all actions by timestamp
        return actions.OrderBy(a => a.Timestamp).ToList();
    }

    /// <summary>
    /// Processes a conversation request - creates new session if SessionId is null, otherwise continues existing session
    /// </summary>
    public async Task<AgentResponse> ProcessConversationRequestAsync(
        ConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check if database provider supports transactions (in-memory doesn't)
        var supportsTransactions = _dbContext.Database.IsRelational();
        
        if (supportsTransactions)
        {
            // Use execution strategy for retry support with transactions
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var response = await ProcessConversationRequestInternalAsync(request, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return response;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
        else
        {
            // In-memory database - no transaction support needed
            return await ProcessConversationRequestInternalAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Internal implementation of conversation request processing (without transaction handling)
    /// </summary>
    private async Task<AgentResponse> ProcessConversationRequestInternalAsync(
        ConversationRequest request,
        CancellationToken cancellationToken)
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
            // Don't save yet - will be saved with actions in ProcessConversationAsync

            _logger.LogInformation("Created new conversation session {SessionId} with title: {Title}", 
                session.Id, request.Title);
        }

        // Process the conversation (saves session and actions atomically)
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
        var systemPrompt = BuildSystemPrompt(session.Title, session.Id, pageState.Url, pageState.FormatType);

        // Get chat completion service
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // Create chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);

        // Add conversation history from previous actions (load from all action tables)
        // Note: Execute queries sequentially as DbContext is not thread-safe for concurrent operations
        var clickActions = await _dbContext.ClickAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "click", Description = $"click on {a.Target}", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        var waitActions = await _dbContext.WaitAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "wait", Description = $"wait {a.Duration}s", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        var completeActions = await _dbContext.CompleteAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "complete", Description = $"complete: {a.Message}", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        var messageActions = await _dbContext.MessageAgentActions
            .Where(a => a.SessionId == session.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new { Type = "message", Description = $"message: {a.Message}", Reasoning = a.Reasoning, CreatedAt = a.CreatedAt })
            .ToListAsync(cancellationToken);

        // Merge, sort chronologically (oldest first), and take the most recent 10
        var allPreviousActions = clickActions
            .Concat(waitActions)
            .Concat(completeActions)
            .Concat(messageActions)
            .OrderBy(a => a.CreatedAt)
            .TakeLast(10)
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
        string pageContext;
        if (pageState.FormatType == PageFormatType.Html && !string.IsNullOrEmpty(pageState.Html))
        {
            pageContext = $"Current page URL: {pageState.Url}\n" +
                        $"HTML content (truncated): {pageState.Html.Substring(0, Math.Min(5000, pageState.Html.Length))}";
        }
        else if (pageState.FormatType == PageFormatType.StructuredJson && pageState.StructuredData != null)
        {
            // For now, pass structured data as JSON string (will be handled properly later)
            var structuredJson = System.Text.Json.JsonSerializer.Serialize(pageState.StructuredData, new JsonSerializerOptions { WriteIndented = false });
            pageContext = $"Current page URL: {pageState.Url}\n" +
                        $"Structured page data: {structuredJson}";
        }
        else
        {
            pageContext = $"Current page URL: {pageState.Url}\n" +
                        $"Page data format: {pageState.FormatType}";
        }
        chatHistory.AddUserMessage($"Current page state:\n{pageContext}");

        // Add user goal (only if this is the first action)
        if (!allPreviousActions.Any())
        {
            chatHistory.AddUserMessage($"User goal: {session.Title}");
        }

        // BrowserPlugin is already registered at startup in ServiceConfiguration

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

        // If no actions were extracted, the model didn't provide valid function calls
        // This shouldn't happen if the model follows instructions, but handle it gracefully
        if (!actions.Any())
        {
            var contentPreview = responseMessage.Content != null 
                ? responseMessage.Content.Substring(0, Math.Min(500, responseMessage.Content.Length))
                : "(null)";
            
            _logger.LogWarning(
                "No actions extracted from model response. Response content: {Content}",
                contentPreview);
            
            // Return a message action explaining the issue
            actions.Add(new MessageAction
            {
                Message = "I received your page state but couldn't determine the next action. Please check the model response format.",
                Reasoning = $"Model response did not contain valid function calls. Response preview: {contentPreview.Substring(0, Math.Min(200, contentPreview.Length))}"
            });
        }

        // Set timestamp for all actions
        var timestamp = DateTime.UtcNow;
        foreach (var action in actions)
        {
            action.Timestamp = timestamp;
        }

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
                        Target = clickAction.XPath, // Target stores XPath expression
                        Reasoning = action.Reasoning ?? string.Empty,
                        Success = false,
                        PageUrl = pageState.Url,
                        PageHtml = TruncateHtml(pageState.Html),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.ClickAgentActions.Add(clickDbAction);
                    break;

                case MessageAction messageAction:
                    var messageDbAction = new MessageAgentAction
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Message = messageAction.Message,
                        Reasoning = action.Reasoning ?? string.Empty,
                        PageUrl = pageState.Url,
                        PageHtml = TruncateHtml(pageState.Html),
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.MessageAgentActions.Add(messageDbAction);
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
        
        // Save all changes atomically (session update + all actions)
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AgentResponse
        {
            SessionId = session.Id,
            Actions = actions,
            Complete = isComplete
        };
    }

    private string BuildSystemPrompt(string userGoal, Guid sessionId, string currentUrl, PageFormatType formatType)
    {
        return $@"You are an AI assistant helping elderly users navigate websites step-by-step.

Your goal: {userGoal}
Current page: {currentUrl}
Session ID: {sessionId}

You have access to the following action types:
- click: Click an element on the page using an XPath expression (e.g., '//button[@id=""login""]', '//a[@href=""/login""]')
- wait: ONLY use when absolutely necessary - when you need to wait for dynamic content to load after an action, animations to finish, or time-based delays. DO NOT use wait for analyzing static page content - the HTML is already provided, analyze it directly.
- message: Display a message to the user (non-terminal - frontend continues sending requests)
- complete: Mark the task as complete (terminal - frontend stops sending requests)

CRITICAL: DO NOT use wait as a default action. The page HTML is already provided to you - analyze it directly and take action. Only wait if:
1. You've clicked something that triggers a loading state that requires time
2. You need to wait for an animation to complete before the next action
3. There's an explicit time-based requirement

IMPORTANT: Use XPath expressions, NOT CSS selectors, for click actions. XPath examples:
- '//button[@id=""submit""]' - button with id=""submit""
- '//a[@href=""/login""]' - link with href=""/login""
- '//input[@type=""text"" and @name=""email""]' - input with type=""text"" and name=""email""
- '//div[@class=""button"" and contains(text(), ""Click me"")]' - div with class=""button"" containing text ""Click me""

CRITICAL INSTRUCTIONS:
1. The page HTML content is ALREADY provided to you in the request - you have the full page state
2. You MUST analyze the HTML directly and take action - do NOT ask for updated page state
3. You MUST respond with valid JSON matching the provided schema
4. The JSON response must contain an ""actions"" array with one or more action objects
5. Each action must have an ""action_type"" field set to one of: ""click"", ""wait"", ""message"", or ""complete""
6. For click actions: include ""xpath"" field with the XPath expression
7. For wait actions: include ""duration"" field with seconds (0-300)
8. For message/complete actions: include ""message"" field with the message text
9. Optionally include ""reasoning"" field to explain why you're taking this action
10. If you cannot find the element you need, use a message action to explain what you're looking for, but DO NOT ask for updated page state
11. The page state is current - analyze it and act on it immediately

Analyze the HTML content provided and determine the next action(s) needed to accomplish the user's goal.
You have the complete page HTML - search through it, find the elements you need, and click them using click actions.
Use message actions only for informational messages or when you need to explain something to the user.
Use complete actions only when the task is fully finished.
ALWAYS respond with valid JSON - never plain text descriptions.";
    }

    /// <summary>
    /// Extracts browser actions from Semantic Kernel's response
    /// Now uses structured JSON output from Gemini instead of regex parsing
    /// </summary>
    private List<BrowserAction> ExtractActionsFromFunctionCalls(ChatMessageContent responseMessage, PageState pageState)
    {
        var actions = new List<BrowserAction>();

        if (string.IsNullOrWhiteSpace(responseMessage.Content))
        {
            _logger.LogWarning("Response message content is empty");
            actions.Add(new MessageAction
            {
                Message = "I received an empty response from the model. The model should return valid JSON with an actions array.",
                Reasoning = "Response content was null or empty"
            });
            return actions;
        }

        try
        {
            // Parse the structured JSON response from Gemini
            // Deserialize to JsonElement first, then manually convert based on action_type
            var jsonDoc = JsonDocument.Parse(responseMessage.Content);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("actions", out var actionsElement) || actionsElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Structured response missing or invalid actions array. Response content: {Content}", 
                    responseMessage.Content.Substring(0, Math.Min(500, responseMessage.Content.Length)));
                
                actions.Add(new MessageAction
                {
                    Message = "I received a response but it contained no actions. The model should return at least one action in the actions array.",
                    Reasoning = "Structured response was valid JSON but missing actions array"
                });
                return actions;
            }

            // Convert each action based on action_type
            foreach (var actionElement in actionsElement.EnumerateArray())
            {
                if (!actionElement.TryGetProperty("action_type", out var actionTypeElement))
                {
                    _logger.LogWarning("Action missing action_type field, skipping");
                    continue;
                }

                var actionType = actionTypeElement.GetString();
                var reasoning = actionElement.TryGetProperty("reasoning", out var reasoningElement) 
                    ? reasoningElement.GetString() 
                    : null;

                BrowserAction? action = actionType switch
                {
                    "click" => actionElement.TryGetProperty("xpath", out var xpathElement)
                        ? new ClickAction
                        {
                            XPath = xpathElement.GetString() ?? string.Empty,
                            Reasoning = reasoning
                        }
                        : null,
                    
                    "wait" => actionElement.TryGetProperty("duration", out var durationElement) && durationElement.TryGetInt32(out var duration)
                        ? new WaitAction
                        {
                            Duration = duration,
                            Reasoning = reasoning
                        }
                        : null,
                    
                    "message" => actionElement.TryGetProperty("message", out var messageElement)
                        ? new MessageAction
                        {
                            Message = messageElement.GetString() ?? string.Empty,
                            Reasoning = reasoning
                        }
                        : null,
                    
                    "complete" => actionElement.TryGetProperty("message", out var completeMessageElement)
                        ? new CompleteAction
                        {
                            Message = completeMessageElement.GetString() ?? string.Empty,
                            Reasoning = reasoning
                        }
                        : null,
                    
                    _ => null
                };

                if (action != null)
                {
                    actions.Add(action);
                }
                else
                {
                    _logger.LogWarning("Unknown or invalid action type: {ActionType}", actionType);
                }
            }

            if (!actions.Any())
            {
                _logger.LogWarning("No valid actions extracted from response");
                actions.Add(new MessageAction
                {
                    Message = "I received a response but couldn't extract any valid actions from it.",
                    Reasoning = "All actions in the response were invalid or missing required fields"
                });
            }
            else
            {
                _logger.LogDebug("Extracted {Count} actions from structured JSON response", actions.Count);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse structured JSON response. Content: {Content}", 
                responseMessage.Content.Substring(0, Math.Min(1000, responseMessage.Content.Length)));
            
            actions.Add(new MessageAction
            {
                Message = "I received a response but couldn't parse it correctly. The model should return valid JSON matching the expected schema.",
                Reasoning = $"JSON parsing error: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing structured response");
            actions.Add(new MessageAction
            {
                Message = "An error occurred while processing the response.",
                Reasoning = $"Unexpected error: {ex.Message}"
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
