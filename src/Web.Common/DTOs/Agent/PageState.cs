using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Represents the current state of a web page
/// </summary>
public class PageState
{
    [Required(ErrorMessage = "Url is required")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Url must be between 1 and 2000 characters")]
    public required string Url { get; set; }

    [Required(ErrorMessage = "Html is required")]
    public required string Html { get; set; }

    /// <summary>
    /// Base64-encoded screenshot (optional)
    /// </summary>
    public string? Screenshot { get; set; }
}
