using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Action to wait for a specified duration
/// </summary>
public class WaitAction : BrowserAction
{
    /// <summary>
    /// Duration in seconds to wait
    /// </summary>
    [Required(ErrorMessage = "Duration is required for wait action")]
    [Range(0, 300, ErrorMessage = "Duration must be between 0 and 300 seconds")]
    public required int Duration { get; set; }
}
