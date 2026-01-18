using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Agent;

/// <summary>
/// Represents structured page data with parsed elements
/// </summary>
public class StructuredPageData
{
    [Required(ErrorMessage = "Url is required")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Url must be between 1 and 2000 characters")]
    public required string Url { get; set; }

    public string? Title { get; set; }

    public string? MetaDescription { get; set; }

    public string? FullText { get; set; }

    public DateTime? Timestamp { get; set; }

    public Viewport? Viewport { get; set; }

    public PageSummary? Summary { get; set; }

    public List<PageElement> Elements { get; set; } = new();
}

/// <summary>
/// Represents the viewport dimensions
/// </summary>
public class Viewport
{
    [Required(ErrorMessage = "Width is required")]
    public int Width { get; set; }

    [Required(ErrorMessage = "Height is required")]
    public int Height { get; set; }
}

/// <summary>
/// Represents summary statistics about the page
/// </summary>
public class PageSummary
{
    public int TotalElements { get; set; }

    public int InteractiveElements { get; set; }

    public int Headings { get; set; }

    public int Links { get; set; }

    public int Buttons { get; set; }

    public int Inputs { get; set; }

    public int Images { get; set; }
}

/// <summary>
/// Represents a single page element
/// </summary>
public class PageElement
{
    [Required(ErrorMessage = "Index is required")]
    public int Index { get; set; }

    public string? Selector { get; set; }

    public string? Tag { get; set; }

    public bool IsVisible { get; set; }

    public bool IsInteractive { get; set; }

    public string? Text { get; set; }

    public string? Href { get; set; }

    public string? Type { get; set; }

    public string? Placeholder { get; set; }
}
