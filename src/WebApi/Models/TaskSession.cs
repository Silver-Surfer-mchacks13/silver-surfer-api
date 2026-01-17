using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

/// <summary>
/// Tracks each user task attempt (e.g., "pay electric bill", "book appointment")
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
    
    public required string Goal { get; set; }
    
    public TaskSessionStatus Status { get; set; } = TaskSessionStatus.InProgress;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
    
    public ICollection<AgentAction> Actions { get; set; } = new List<AgentAction>();
}

/// <summary>
/// Status of a task session
/// </summary>
public enum TaskSessionStatus
{
    InProgress,
    Completed,
    Failed,
    Cancelled
}
