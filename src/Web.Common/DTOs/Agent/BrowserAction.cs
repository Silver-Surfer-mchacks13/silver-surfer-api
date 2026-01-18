using System.Text.Json.Serialization;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Base class for all browser actions with polymorphic JSON serialization
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "action_type")]
[JsonDerivedType(typeof(ClickAction), "click")]
[JsonDerivedType(typeof(WaitAction), "wait")]
[JsonDerivedType(typeof(CompleteAction), "complete")]
[JsonDerivedType(typeof(MessageAction), "message")]
public abstract class BrowserAction
{
    /// <summary>
    /// Timestamp when this action was created (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Reasoning for this action (optional, for debugging/logging)
    /// </summary>
    public string? Reasoning { get; set; }
}
