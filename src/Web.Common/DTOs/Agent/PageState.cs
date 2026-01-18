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

    /// <summary>
    /// HTML content (required when FormatType is Html)
    /// </summary>
    public string? Html { get; set; }

    /// <summary>
    /// Structured page data (required when FormatType is StructuredJson)
    /// </summary>
    public StructuredPageData? StructuredData { get; set; }

    /// <summary>
    /// Format type of the page data
    /// </summary>
    public PageFormatType FormatType { get; set; }

    /// <summary>
    /// Base64-encoded screenshot (optional)
    /// </summary>
    public string? Screenshot { get; set; }
}
