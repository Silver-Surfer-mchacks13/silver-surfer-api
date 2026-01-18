using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

/// <summary>
/// Represents a conversation session (like a chat conversation in modern LLM UIs)
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class TaskSession
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// User ID (nullable for now, will be required when authorization is added)
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// Title of the conversation (e.g., "Pay electric bill", "Book appointment")
    /// </summary>
    public required string Title { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// When the conversation was completed (null if still active)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
    
    public ICollection<ClickAgentAction> ClickActions { get; set; } = new List<ClickAgentAction>();
    public ICollection<WaitAgentAction> WaitActions { get; set; } = new List<WaitAgentAction>();
    public ICollection<CompleteAgentAction> CompleteActions { get; set; } = new List<CompleteAgentAction>();
    public ICollection<MessageAgentAction> MessageActions { get; set; } = new List<MessageAgentAction>();
}
