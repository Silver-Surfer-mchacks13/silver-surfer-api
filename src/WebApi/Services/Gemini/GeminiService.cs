using System.Net.Http;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Web.Common.DTOs.Analyze;
using WebApi.Configuration.Options;

namespace WebApi.Services.Gemini;

/// <summary>
/// Service for analyzing web pages using Google Gemini via Vertex AI
/// </summary>
public class GeminiService
{
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public GeminiService(IOptions<GeminiOptions> options, ILogger<GeminiService> logger, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        // Use generateContent endpoint for Gemini (not predict)
        _baseUrl = $"https://{_options.Location}-aiplatform.googleapis.com/v1/projects/{_options.ProjectId}/locations/{_options.Location}/publishers/google/models/{_options.Model}:generateContent";
        
        // Configure HttpClient timeout
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<AnalyzeResponse> AnalyzeWebPageAsync(
        AnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Starting Gemini analysis. Prompt length: {PromptLength}, HTML length: {HtmlLength}",
                request.Prompt.Length,
                request.HtmlContent.Length);

            // Validate and clean base64 image
            var cleanBase64 = CleanBase64String(request.ImageBase64);
            if (string.IsNullOrWhiteSpace(cleanBase64))
            {
                return new AnalyzeResponse
                {
                    Success = false,
                    Error = "Invalid base64 image data"
                };
            }

            // Build the combined prompt
            var combinedPrompt = BuildPrompt(request);

            // Create request payload for Vertex AI Gemini generateContent API
            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = combinedPrompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = request.ImageMimeType,
                                    data = cleanBase64
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = _options.MaxTokens,
                    temperature = _options.Temperature,
                    topP = 0.95,
                    topK = 40
                }
            };

            // Get access token for authentication
            var accessToken = await GetAccessTokenAsync(cancellationToken);

            // Create HTTP request
            var jsonPayload = JsonSerializer.Serialize(requestPayload);
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
                return new AnalyzeResponse
                {
                    Success = false,
                    Error = $"API request failed: {httpResponse.StatusCode}"
                };
            }

            // Parse response
            var responseJson = JsonDocument.Parse(responseContent);
            var resultText = ExtractTextFromResponse(responseJson.RootElement);
            var tokensUsed = ExtractTokenCount(responseJson.RootElement);

            stopwatch.Stop();
            _logger.LogInformation(
                "Gemini analysis completed in {ElapsedMs}ms. Response length: {ResponseLength}, Tokens: {Tokens}",
                stopwatch.ElapsedMilliseconds,
                resultText?.Length ?? 0,
                tokensUsed);

            return new AnalyzeResponse
            {
                Success = true,
                Result = resultText,
                TokensUsed = tokensUsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during Gemini analysis after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return new AnalyzeResponse
            {
                Success = false,
                Error = $"Analysis failed: {ex.Message}"
            };
        }
    }

    private string BuildPrompt(AnalyzeRequest request)
    {
        return $@"You are analyzing a webpage. The user has provided:

1. A screenshot of the page
2. The HTML content of the page
3. A specific request/question

User's Request:
{request.Prompt}

HTML Content:
```html
{request.HtmlContent}
```

Please analyze the screenshot in combination with the HTML content and respond to the user's request.";
    }

    private static string CleanBase64String(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
            return string.Empty;

        // Remove data URL prefix if present (e.g., "data:image/png;base64,")
        if (base64String.Contains(","))
        {
            base64String = base64String.Split(',')[1];
        }

        // Validate it's valid base64
        try
        {
            Convert.FromBase64String(base64String);
            return base64String;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Use Application Default Credentials (ADC)
        // For local dev: gcloud auth application-default login
        // For production: Set GOOGLE_APPLICATION_CREDENTIALS environment variable
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

    private static int? ExtractTokenCount(JsonElement root)
    {
        try
        {
            // Token usage is in usageMetadata for generateContent API
            if (root.TryGetProperty("usageMetadata", out var usageMetadata))
            {
                if (usageMetadata.TryGetProperty("totalTokenCount", out var tokenCount))
                {
                    return tokenCount.GetInt32();
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
