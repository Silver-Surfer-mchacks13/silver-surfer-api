using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Action to display a message to the user (non-terminal - frontend should continue sending requests)
/// </summary>
public class MessageAction : BrowserAction
{
    /// <summary>
    /// Message to display to the user
    /// </summary>
    [Required(ErrorMessage = "Message is required for message action")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 1000 characters")]
    public required string Message { get; set; }
}
