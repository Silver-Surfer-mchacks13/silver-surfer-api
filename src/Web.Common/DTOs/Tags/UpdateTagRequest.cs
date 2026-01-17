using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WebApi.DTOs.Tags;

public class UpdateTagRequest
{
    [Required(ErrorMessage = "Tag name is required")]
    [StringLength(50, ErrorMessage = "Tag name must not exceed 50 characters")]
    public required string Name { get; set; }
    
    [Required(ErrorMessage = "Color is required")]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be in hex format (#RRGGBB)")]
    public required string Color { get; set; }
}


