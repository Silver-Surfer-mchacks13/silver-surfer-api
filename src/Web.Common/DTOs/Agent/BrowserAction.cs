using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Represents a browser action to be executed (click, type, navigate, etc.)
/// </summary>
public class BrowserAction
{
    [Required(ErrorMessage = "Action is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Action must be between 1 and 50 characters")]
    public required string Action { get; set; } // click, type, navigate, wait, scroll, complete

    /// <summary>
    /// CSS selector or target element (nullable for actions like "wait" or "complete")
    /// </summary>
    [StringLength(500, ErrorMessage = "Target must not exceed 500 characters")]
    public string? Target { get; set; }

    /// <summary>
    /// Value to type or other action-specific data (nullable)
    /// </summary>
    [StringLength(2000, ErrorMessage = "Value must not exceed 2000 characters")]
    public string? Value { get; set; }

    /// <summary>
    /// Duration in seconds (for wait/scroll actions)
    /// </summary>
    [Range(0, 300, ErrorMessage = "Duration must be between 0 and 300 seconds")]
    public int? Duration { get; set; }

    /// <summary>
    /// Reasoning for this action (optional, for debugging/logging)
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Whether the previous action was successful (for ContinueConversationRequest)
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// Error message if the previous action failed (for ContinueConversationRequest)
    /// </summary>
    public string? ErrorMessage { get; set; }
}
