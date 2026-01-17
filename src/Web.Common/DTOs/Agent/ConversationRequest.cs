using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Unified request for creating or continuing a conversation
/// </summary>
public class ConversationRequest
{
    /// <summary>
    /// Title of the conversation (required only for new conversations)
    /// </summary>
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 1000 characters")]
    public string? Title { get; set; }

    /// <summary>
    /// Session ID for continuing an existing conversation (optional - omit for new conversation)
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Current state of the browser page
    /// </summary>
    [Required(ErrorMessage = "PageState is required")]
    public required PageState PageState { get; set; }
}
