using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Web.Common.DTOs.Agent;
using WebApi.Tests.Helpers;

namespace WebApi.Tests.Controllers;

[TestFixture]
public class AgentControllerTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory(useTestAuth: false);
        _client = _factory.CreateUnauthenticatedClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Conversation_ThreeStepFlow_ReturnsResponsesAtEachStep()
    {
        // Arrange - Load test data files
        var step1Data = LoadTestData("1", "1_home_page");
        var step2Data = LoadTestData("1", "2_cart");
        var step3Data = LoadTestData("1", "3_checkout");

        // Create output directory for request files
        var outputDir = Path.Combine(Path.GetTempPath(), "AgentTestRequests", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(outputDir);
        var requestFiles = new List<string>();

        // Step 1: Create conversation with home page (no SessionId = new conversation)
        var request1 = new ConversationRequest
        {
            Title = "Complete checkout on Amazon",
            SessionId = null, // New conversation
            PageState = new PageState
            {
                Url = step1Data.Url,
                Html = step1Data.Html,
                Screenshot = step1Data.Screenshot
            }
        };

        // Save request body to file
        var requestFile1 = SaveRequestToFile(outputDir, "POST_api_v1_agent_conversations", request1);
        requestFiles.Add(requestFile1);

        // Act - Step 1
        PrintStepInput("Step 1: Create Conversation", request1);
        var response1 = await _client.PostAsJsonSnakeCaseAsync("/api/v1/agent/conversations", request1);
        
        // Assert - Step 1
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var result1 = await response1.Content.ReadFromJsonSnakeCaseAsync<AgentResponse>();
        result1.Should().NotBeNull();
        result1!.SessionId.Should().NotBeEmpty();
        result1.Actions.Should().NotBeEmpty();
        result1.Complete.Should().BeFalse(); // Not complete yet

        PrintStepOutput("Step 1: Create Conversation", result1);
        var sessionId = result1.SessionId;

        // Step 2: Continue conversation with cart page (with SessionId)
        var request2 = new ConversationRequest
        {
            SessionId = sessionId, // Continue existing conversation
            PageState = new PageState
            {
                Url = step2Data.Url,
                Html = step2Data.Html,
                Screenshot = step2Data.Screenshot
            }
        };

        // Save request body to file
        var requestFile2 = SaveRequestToFile(outputDir, $"POST_api_v1_agent_conversations", request2);
        requestFiles.Add(requestFile2);

        // Act - Step 2
        PrintStepInput("Step 2: Continue Conversation (Cart)", request2);
        var response2 = await _client.PostAsJsonSnakeCaseAsync("/api/v1/agent/conversations", request2);

        // Assert - Step 2
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var result2 = await response2.Content.ReadFromJsonSnakeCaseAsync<AgentResponse>();
        result2.Should().NotBeNull();
        result2!.SessionId.Should().Be(sessionId); // Same session
        result2.Actions.Should().NotBeEmpty();
        result2.Complete.Should().BeFalse(); // Still not complete

        PrintStepOutput("Step 2: Continue Conversation (Cart)", result2);

        // Step 3: Continue conversation with checkout page (with SessionId)
        var request3 = new ConversationRequest
        {
            SessionId = sessionId, // Continue existing conversation
            PageState = new PageState
            {
                Url = step3Data.Url,
                Html = step3Data.Html,
                Screenshot = step3Data.Screenshot
            }
        };

        // Save request body to file
        var requestFile3 = SaveRequestToFile(outputDir, $"POST_api_v1_agent_conversations", request3);
        requestFiles.Add(requestFile3);

        // Act - Step 3
        PrintStepInput("Step 3: Continue Conversation (Checkout)", request3);
        var response3 = await _client.PostAsJsonSnakeCaseAsync("/api/v1/agent/conversations", request3);

        // Assert - Step 3
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
        var result3 = await response3.Content.ReadFromJsonSnakeCaseAsync<AgentResponse>();
        result3.Should().NotBeNull();
        result3!.SessionId.Should().Be(sessionId); // Same session
        result3.Actions.Should().NotBeEmpty();
        // Complete might be true or false depending on the AI's response

        PrintStepOutput("Step 3: Continue Conversation (Checkout)", result3);

        // Print request file locations
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("REQUEST FILES SAVED:");
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"Output Directory: {outputDir}");
        Console.WriteLine();
        foreach (var file in requestFiles)
        {
            Console.WriteLine($"  - {file}");
        }
        Console.WriteLine(new string('=', 80));
    }

    private TestData LoadTestData(string scenario, string step)
    {
        // Get the base directory (where the test assembly is located)
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var dataPath = Path.Combine(baseDirectory, "data", scenario, step);

        var urlPath = Path.Combine(dataPath, "url.txt");
        var htmlPath = Path.Combine(dataPath, "html.txt");
        var screenshotPath = Path.Combine(dataPath, "ss_b64.txt");

        return new TestData
        {
            Url = File.Exists(urlPath) ? File.ReadAllText(urlPath).Trim() : string.Empty,
            Html = File.Exists(htmlPath) ? File.ReadAllText(htmlPath) : string.Empty,
            Screenshot = File.Exists(screenshotPath) ? File.ReadAllText(screenshotPath).Trim() : null
        };
    }

    private void PrintStepInput(string stepName, object request)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine($"INPUT: {stepName}");
        Console.WriteLine(new string('-', 80));

        if (request is ConversationRequest conversationRequest)
        {
            Console.WriteLine($"SessionId: {conversationRequest.SessionId?.ToString() ?? "(new conversation)"}");
            if (!string.IsNullOrEmpty(conversationRequest.Title))
            {
                Console.WriteLine($"Title: {conversationRequest.Title}");
            }
            Console.WriteLine($"URL: {conversationRequest.PageState.Url}");
            Console.WriteLine($"HTML Length: {conversationRequest.PageState.Html.Length} chars");
            Console.WriteLine($"HTML (truncated to 500 chars):");
            Console.WriteLine(TruncateString(conversationRequest.PageState.Html, 500));
            if (!string.IsNullOrEmpty(conversationRequest.PageState.Screenshot))
            {
                Console.WriteLine($"Screenshot (base64, truncated): {TruncateString(conversationRequest.PageState.Screenshot, 100)}...");
            }
        }

        Console.WriteLine(new string('=', 80));
    }

    private void PrintStepOutput(string stepName, AgentResponse response)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine($"OUTPUT: {stepName}");
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"SessionId: {response.SessionId}");
        Console.WriteLine($"Complete: {response.Complete}");
        Console.WriteLine($"Actions Count: {response.Actions.Count}");
        
        for (int i = 0; i < response.Actions.Count; i++)
        {
            var action = response.Actions[i];
            Console.WriteLine($"  Action {i + 1}:");
            
            string actionType = action switch
            {
                ClickAction => "click",
                WaitAction => "wait",
                MessageAction => "message",
                CompleteAction => "complete",
                _ => "unknown"
            };
            Console.WriteLine($"    - Type: {actionType}");
            
            switch (action)
            {
                case ClickAction clickAction:
                    Console.WriteLine($"    - XPath: {clickAction.XPath}");
                    break;
                case WaitAction waitAction:
                    Console.WriteLine($"    - Duration: {waitAction.Duration} seconds");
                    break;
                case MessageAction messageAction:
                    Console.WriteLine($"    - Message: {TruncateString(messageAction.Message, 100)}");
                    break;
                case CompleteAction completeAction:
                    Console.WriteLine($"    - Message: {TruncateString(completeAction.Message, 100)}");
                    break;
            }
            
            Console.WriteLine($"    - Reasoning: {TruncateString(action.Reasoning ?? "(none)", 100)}");
        }

        Console.WriteLine(new string('-', 80));
        Console.WriteLine("Raw JSON:");
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var rawJson = JsonSerializer.Serialize(response, jsonOptions);
        Console.WriteLine(rawJson);
        Console.WriteLine(new string('=', 80));
    }

    private static string TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";

        if (value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength) + $"... [truncated, total length: {value.Length}]";
    }

    private string SaveRequestToFile(string outputDir, string endpoint, object request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var safeEndpoint = endpoint.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
        var filename = $"{timestamp}_{safeEndpoint}.json";
        var filePath = Path.Combine(outputDir, filename);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var json = JsonSerializer.Serialize(request, options);
        File.WriteAllText(filePath, json, Encoding.UTF8);

        return filePath;
    }

    private class TestData
    {
        public string Url { get; set; } = string.Empty;
        public string Html { get; set; } = string.Empty;
        public string? Screenshot { get; set; }
    }
}
