using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Action to click an element on the page
/// </summary>
public class ClickAction : BrowserAction
{
    /// <summary>
    /// XPath expression for the element to click
    /// </summary>
    [Required(ErrorMessage = "XPath is required for click action")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "XPath must be between 1 and 500 characters")]
    public required string XPath { get; set; }
}
