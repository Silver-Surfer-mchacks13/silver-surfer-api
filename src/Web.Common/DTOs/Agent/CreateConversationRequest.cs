using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Request to create a new conversation/session
/// </summary>
public class CreateConversationRequest
{
    [Required(ErrorMessage = "UserGoal is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "UserGoal must be between 1 and 1000 characters")]
    public required string UserGoal { get; set; }

    [Required(ErrorMessage = "PageState is required")]
    public required PageState PageState { get; set; }
}
