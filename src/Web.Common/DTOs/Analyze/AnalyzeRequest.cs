using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Analyze;

public class AnalyzeRequest
{
    [Required(ErrorMessage = "Prompt is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Prompt must be between 1 and 10000 characters")]
    public required string Prompt { get; set; }

    [Required(ErrorMessage = "HtmlContent is required")]
    public required string HtmlContent { get; set; }

    [Required(ErrorMessage = "ImageBase64 is required")]
    public required string ImageBase64 { get; set; }

    public string ImageMimeType { get; set; } = "image/png";
}
