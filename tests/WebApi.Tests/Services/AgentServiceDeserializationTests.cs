using System.Text.Json;
using FluentAssertions;
using Web.Common.DTOs.Agent;

namespace WebApi.Tests.Services;

[TestFixture]
public class AgentServiceDeserializationTests
{
    /// <summary>
    /// Test deserialization using the exact same logic as AgentService.ExtractActionsFromFunctionCalls
    /// This ensures our deserialization works correctly with real Gemini responses
    /// </summary>
    [Test]
    public void DeserializeGeminiResponse_MessageAction_ShouldParseCorrectly()
    {
        // Arrange - Actual response from Gemini
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""action_type"": ""message"",
      ""message"": ""It looks like your shopping cart is empty. To check out, we first need to add an item to the cart. What would you like to buy today?"",
      ""reasoning"": ""The user's goal is to complete checkout, but the shopping cart is currently empty, as indicated by the cart icon showing '0' items. Before proceeding to checkout, an item must be added to the cart. I am asking the user what they would like to purchase so I can assist them in adding it to the cart.""
    }
  ]
}";

        // Act - Use the same deserialization logic as AgentService
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().HaveCount(1);
        var messageAction = actions[0].Should().BeOfType<MessageAction>().Subject;
        messageAction.Message.Should().Be("It looks like your shopping cart is empty. To check out, we first need to add an item to the cart. What would you like to buy today?");
        messageAction.Reasoning.Should().Contain("shopping cart is currently empty");
    }

    [Test]
    public void DeserializeGeminiResponse_ClickAction_ShouldParseCorrectly()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""action_type"": ""click"",
      ""xpath"": ""//a[@id=\""nav-cart\""]"",
      ""reasoning"": ""Clicking on the cart icon to begin the checkout process.""
    }
  ]
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().HaveCount(1);
        var clickAction = actions[0].Should().BeOfType<ClickAction>().Subject;
        clickAction.XPath.Should().Be("//a[@id=\"nav-cart\"]");
        clickAction.Reasoning.Should().Be("Clicking on the cart icon to begin the checkout process.");
    }

    [Test]
    public void DeserializeGeminiResponse_WaitAction_ShouldParseCorrectly()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""action_type"": ""wait"",
      ""duration"": 2,
      ""reasoning"": ""Waiting for page to load after clicking submit button""
    }
  ]
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().HaveCount(1);
        var waitAction = actions[0].Should().BeOfType<WaitAction>().Subject;
        waitAction.Duration.Should().Be(2);
        waitAction.Reasoning.Should().Be("Waiting for page to load after clicking submit button");
    }

    [Test]
    public void DeserializeGeminiResponse_CompleteAction_ShouldParseCorrectly()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""action_type"": ""complete"",
      ""message"": ""Successfully completed checkout process"",
      ""reasoning"": ""The checkout process has been completed successfully""
    }
  ]
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().HaveCount(1);
        var completeAction = actions[0].Should().BeOfType<CompleteAction>().Subject;
        completeAction.Message.Should().Be("Successfully completed checkout process");
        completeAction.Reasoning.Should().Be("The checkout process has been completed successfully");
    }

    [Test]
    public void DeserializeGeminiResponse_MultipleActions_ShouldParseAll()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""action_type"": ""click"",
      ""xpath"": ""//button[@id=\""submit\""]"",
      ""reasoning"": ""Clicking submit button""
    },
    {
      ""action_type"": ""wait"",
      ""duration"": 1,
      ""reasoning"": ""Waiting for form submission""
    }
  ]
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Should().BeOfType<ClickAction>();
        actions[1].Should().BeOfType<WaitAction>();
    }

    [Test]
    public void DeserializeGeminiResponse_MissingActionType_ShouldSkipInvalidAction()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""xpath"": ""//button"",
      ""reasoning"": ""Missing action_type""
    },
    {
      ""action_type"": ""click"",
      ""xpath"": ""//button[@id=\""valid\""]""
    }
  ]
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert - Should only parse the valid action
        actions.Should().HaveCount(1);
        actions[0].Should().BeOfType<ClickAction>();
    }

    [Test]
    public void DeserializeGeminiResponse_UnknownActionType_ShouldReturnEmpty()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": [
    {
      ""action_type"": ""unknown_type"",
      ""some_field"": ""value""
    }
  ]
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().BeEmpty();
    }

    [Test]
    public void DeserializeGeminiResponse_EmptyActionsArray_ShouldReturnEmpty()
    {
        // Arrange
        var geminiResponseText = @"{
  ""actions"": []
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().BeEmpty();
    }

    [Test]
    public void DeserializeGeminiResponse_MissingActionsProperty_ShouldReturnEmpty()
    {
        // Arrange
        var geminiResponseText = @"{
  ""other_field"": ""value""
}";

        // Act
        var actions = DeserializeActions(geminiResponseText);

        // Assert
        actions.Should().BeEmpty();
    }

    /// <summary>
    /// Replicates the exact deserialization logic from AgentService.ExtractActionsFromFunctionCalls
    /// </summary>
    private List<BrowserAction> DeserializeActions(string responseContent)
    {
        var actions = new List<BrowserAction>();

        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return actions;
        }

        try
        {
            // Parse the structured JSON response from Gemini
            // Deserialize to JsonElement first, then manually convert based on action_type
            var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("actions", out var actionsElement) || actionsElement.ValueKind != JsonValueKind.Array)
            {
                return actions;
            }

            // Convert each action based on action_type
            foreach (var actionElement in actionsElement.EnumerateArray())
            {
                if (!actionElement.TryGetProperty("action_type", out var actionTypeElement))
                {
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
            }
        }
        catch (JsonException)
        {
            // Return empty list on JSON parsing errors (same as AgentService would handle)
        }
        catch (Exception)
        {
            // Return empty list on other errors
        }

        return actions;
    }
}
