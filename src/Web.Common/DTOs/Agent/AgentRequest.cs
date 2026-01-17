using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Request from browser extension to process agent action
/// </summary>
public class AgentRequest
{
    /// <summary>
    /// Session ID (null = new session)
    /// </summary>
    public Guid? SessionId { get; set; }

    [Required(ErrorMessage = "UserGoal is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "UserGoal must be between 1 and 1000 characters")]
    public required string UserGoal { get; set; }

    [Required(ErrorMessage = "PageState is required")]
    public required PageState PageState { get; set; }

    /// <summary>
    /// Result of the previous action (if any)
    /// </summary>
    public BrowserAction? PreviousAction { get; set; }
}
