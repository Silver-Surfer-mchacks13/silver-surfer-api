using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

/// <summary>
/// Records every action taken by the AI agent during a task session
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class AgentAction
{
    public Guid Id { get; set; }
    
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// Type of action: click, type, navigate, wait, scroll, complete
    /// </summary>
    public required string ActionType { get; set; }
    
    /// <summary>
    /// CSS selector or target element (nullable for actions like "wait" or "complete")
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// Value to type or other action-specific data (nullable)
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// AI's explanation for why this action was taken
    /// </summary>
    public required string Reasoning { get; set; }
    
    /// <summary>
    /// Whether the action was successful (set by extension after execution)
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if action failed (nullable)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// URL of the page where action was taken
    /// </summary>
    public required string PageUrl { get; set; }
    
    /// <summary>
    /// HTML content of the page (truncated, nullable for storage efficiency)
    /// </summary>
    public string? PageHtml { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public TaskSession Session { get; set; } = null!;
}
