using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

/// <summary>
/// Database model for message actions
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class MessageAgentAction
{
    public Guid Id { get; set; }
    
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// Message to display to the user
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// AI's explanation for why this action was taken
    /// </summary>
    public required string Reasoning { get; set; }
    
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
