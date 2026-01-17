using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Request to continue an existing conversation
/// </summary>
public class ContinueConversationRequest
{
    [Required(ErrorMessage = "PageState is required")]
    public required PageState PageState { get; set; }
}
