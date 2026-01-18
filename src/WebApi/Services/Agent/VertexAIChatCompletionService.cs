using System.Net.Http;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using WebApi.Configuration.Options;

namespace WebApi.Services.Agent;

/// <summary>
/// Custom chat completion service that wraps Vertex AI Gemini for use with Semantic Kernel
/// </summary>
public class VertexAIChatCompletionService : IChatCompletionService
{
    private readonly GeminiOptions _options;
    private readonly ILogger<VertexAIChatCompletionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string? ModelId => _options.Model;
    
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public VertexAIChatCompletionService(
        IOptions<GeminiOptions> options,
        ILogger<VertexAIChatCompletionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _baseUrl = $"https://{_options.Location}-aiplatform.googleapis.com/v1/projects/{_options.ProjectId}/locations/{_options.Location}/publishers/google/models/{_options.Model}:generateContent";
        
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // For now, we'll implement non-streaming and convert to streaming
        // Full streaming support can be added later if needed
        var messages = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        
        foreach (var message in messages)
        {
            // Create streaming message from regular message
            var streamingMessage = new StreamingChatMessageContent(
                role: message.Role,
                content: message.Content,
                modelId: message.ModelId,
                innerContent: message.InnerContent,
                metadata: message.Metadata);
            
            yield return streamingMessage;
        }
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert ChatHistory to Vertex AI format
            // Note: Vertex AI Gemini doesn't support "system" role, so we convert system messages to user messages
            var contents = new List<object>();
            var systemPrompts = new List<string>();
            
            foreach (var message in chatHistory)
            {
                if (message.Role.Label == "system")
                {
                    // Collect system messages to prepend to first user message
                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        systemPrompts.Add(message.Content);
                    }
                }
                else
                {
                    var role = message.Role.Label switch
                    {
                        "user" => "user",
                        "assistant" => "model",
                        _ => "user"
                    };
                    
                    // If this is the first user message and we have system prompts, prepend them
                    var messageText = message.Content ?? string.Empty;
                    if (role == "user" && systemPrompts.Any() && contents.Count == 0)
                    {
                        messageText = string.Join("\n\n", systemPrompts) + "\n\n" + messageText;
                        systemPrompts.Clear(); // Clear after using
                    }
                    
                    contents.Add(new
                    {
                        role = role,
                        parts = new[]
                        {
                            new { text = messageText }
                        }
                    });
                }
            }
            
            // If we have system prompts but no user messages yet, create a user message with just the system prompt
            if (systemPrompts.Any() && contents.Count == 0)
            {
                contents.Add(new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = string.Join("\n\n", systemPrompts) }
                    }
                });
            }

            // Build JSON schema for structured output
            var responseSchema = new
            {
                type = "object",
                properties = new
                {
                    actions = new
                    {
                        type = "array",
                        description = "List of actions to execute",
                        items = new
                        {
                            oneOf = new object[]
                            {
                                new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        action_type = new { type = "string", @enum = new[] { "click" } },
                                        xpath = new { type = "string", description = "XPath expression for the element to click", maxLength = 500 },
                                        reasoning = new { type = "string", description = "Optional explanation of why this action is being taken" }
                                    },
                                    required = new[] { "action_type", "xpath" },
                                    additionalProperties = false
                                },
                                new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        action_type = new { type = "string", @enum = new[] { "wait" } },
                                        duration = new { type = "integer", description = "Duration in seconds to wait", minimum = 0, maximum = 300 },
                                        reasoning = new { type = "string", description = "Optional explanation of why waiting is necessary" }
                                    },
                                    required = new[] { "action_type", "duration" },
                                    additionalProperties = false
                                },
                                new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        action_type = new { type = "string", @enum = new[] { "message" } },
                                        message = new { type = "string", description = "Message to display to the user", maxLength = 1000 },
                                        reasoning = new { type = "string", description = "Optional explanation of why this message is being shown" }
                                    },
                                    required = new[] { "action_type", "message" },
                                    additionalProperties = false
                                },
                                new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        action_type = new { type = "string", @enum = new[] { "complete" } },
                                        message = new { type = "string", description = "Message summarizing what was accomplished", maxLength = 1000 },
                                        reasoning = new { type = "string", description = "Optional explanation of why the task is complete" }
                                    },
                                    required = new[] { "action_type", "message" },
                                    additionalProperties = false
                                }
                            }
                        }
                    }
                },
                required = new[] { "actions" },
                additionalProperties = false
            };

            // Build request payload with structured output
            var requestPayload = new
            {
                contents = contents,
                generationConfig = new
                {
                    maxOutputTokens = _options.MaxTokens,
                    temperature = _options.Temperature,
                    topP = 0.95,
                    topK = 40,
                    responseMimeType = "application/json",
                    responseSchema = responseSchema
                }
            };

            // Get access token
            var accessToken = await GetAccessTokenAsync(cancellationToken);

            // Create HTTP request
            var jsonPayload = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = true });
            
            // Log the exact request payload for debugging
            _logger.LogInformation("Sending request to Vertex AI Gemini:\n{RequestPayload}", jsonPayload);
            
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Call Vertex AI API
            var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Vertex AI API returned error: {StatusCode} - {Response}", 
                    httpResponse.StatusCode, responseContent);
                
                // Provide more helpful error message for 404 (model not found)
                if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new HttpRequestException(
                        $"Vertex AI model '{_options.Model}' not found in project '{_options.ProjectId}' at location '{_options.Location}'. " +
                        $"Please verify the model name is correct and available in your region. " +
                        $"Common models: gemini-2.0-flash-exp, gemini-2.0-flash, gemini-2.5-flash, gemini-2.5-pro. " +
                        $"Original error: {responseContent}");
                }
                
                throw new HttpRequestException($"Vertex AI API request failed: {httpResponse.StatusCode} - {responseContent}");
            }

            // Parse response
            var responseJson = JsonDocument.Parse(responseContent);
            
            // Log the raw response for debugging
            _logger.LogInformation("Received response from Vertex AI Gemini:\n{ResponseContent}", responseContent);
            
            var resultText = ExtractTextFromResponse(responseJson.RootElement);

            if (string.IsNullOrEmpty(resultText))
            {
                _logger.LogWarning("No text content extracted from response. Full response: {Response}", responseContent);
                throw new InvalidOperationException("No text content in Vertex AI response");
            }
            
            _logger.LogDebug("Extracted text from response: {Text}", resultText);

            // Return as ChatMessageContent
            var chatMessage = new ChatMessageContent(
                AuthorRole.Assistant,
                resultText,
                modelId: _options.Model);

            return new[] { chatMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vertex AI Gemini chat completion");
            throw;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Use Application Default Credentials (ADC)
        var credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
        if (credential.IsCreateScopedRequired)
        {
            credential = credential.CreateScoped(new[] { "https://www.googleapis.com/auth/cloud-platform" });
        }
        var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        return accessToken;
    }

    private static string? ExtractTextFromResponse(JsonElement root)
    {
        try
        {
            // Vertex AI Gemini generateContent response structure:
            // candidates[0].content.parts[0].text
            if (root.TryGetProperty("candidates", out var candidates) && 
                candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("text", out var text))
                    {
                        return text.GetString();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
