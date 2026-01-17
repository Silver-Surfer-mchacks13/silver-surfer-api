using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Action indicating the task is complete
/// </summary>
public class CompleteAction : BrowserAction
{
    /// <summary>
    /// Message summarizing what was accomplished
    /// </summary>
    [Required(ErrorMessage = "Message is required for complete action")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 1000 characters")]
    public required string Message { get; set; }
}
