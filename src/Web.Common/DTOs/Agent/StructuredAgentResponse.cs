using System.Text.Json.Serialization;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Structured response format from Gemini that matches the JSON schema
/// </summary>
public class StructuredAgentResponse
{
    /// <summary>
    /// List of actions to execute
    /// </summary>
    [JsonPropertyName("actions")]
    public required List<StructuredBrowserAction> Actions { get; set; }
}

/// <summary>
/// Structured browser action that can be one of several types
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "action_type")]
[JsonDerivedType(typeof(StructuredClickAction), "click")]
[JsonDerivedType(typeof(StructuredWaitAction), "wait")]
[JsonDerivedType(typeof(StructuredCompleteAction), "complete")]
[JsonDerivedType(typeof(StructuredMessageAction), "message")]
public abstract class StructuredBrowserAction
{
    /// <summary>
    /// Type of action (click, wait, message, complete)
    /// </summary>
    [JsonPropertyName("action_type")]
    public required string ActionType { get; set; }

    /// <summary>
    /// Reasoning for this action (optional)
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}

/// <summary>
/// Structured click action
/// </summary>
public class StructuredClickAction : StructuredBrowserAction
{
    /// <summary>
    /// XPath expression for the element to click
    /// </summary>
    [JsonPropertyName("xpath")]
    public required string XPath { get; set; }
}

/// <summary>
/// Structured wait action
/// </summary>
public class StructuredWaitAction : StructuredBrowserAction
{
    /// <summary>
    /// Duration in seconds to wait
    /// </summary>
    [JsonPropertyName("duration")]
    public required int Duration { get; set; }
}

/// <summary>
/// Structured message action
/// </summary>
public class StructuredMessageAction : StructuredBrowserAction
{
    /// <summary>
    /// Message to display to the user
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }
}

/// <summary>
/// Structured complete action
/// </summary>
public class StructuredCompleteAction : StructuredBrowserAction
{
    /// <summary>
    /// Message summarizing what was accomplished
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }
}
