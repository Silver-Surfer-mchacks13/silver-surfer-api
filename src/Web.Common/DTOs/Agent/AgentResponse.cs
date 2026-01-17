namespace Web.Common.DTOs.Agent;

/// <summary>
/// Response from agent service with actions to execute
/// </summary>
public class AgentResponse
{
    public Guid SessionId { get; set; }

    public required List<BrowserAction> Actions { get; set; }

    public required string Reasoning { get; set; }

    /// <summary>
    /// Whether the task is complete
    /// </summary>
    public bool Complete { get; set; }

    /// <summary>
    /// Message if AI needs user input/clarification
    /// </summary>
    public string? NeedsUserInput { get; set; }
}
