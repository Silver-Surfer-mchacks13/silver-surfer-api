using FluentValidation;
using Web.Common.DTOs.Analyze;

namespace WebApi.Validators;

public class AnalyzeRequestValidator : AbstractValidator<AnalyzeRequest>
{
    public AnalyzeRequestValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Prompt is required")
            .MaximumLength(10000).WithMessage("Prompt must not exceed 10000 characters");

        RuleFor(x => x.HtmlContent)
            .NotEmpty().WithMessage("HtmlContent is required");

        RuleFor(x => x.ImageBase64)
            .NotEmpty().WithMessage("ImageBase64 is required")
            .Must(BeValidBase64).WithMessage("ImageBase64 must be a valid base64 string");

        RuleFor(x => x.ImageMimeType)
            .Must(BeValidMimeType).WithMessage("ImageMimeType must be a valid image MIME type (e.g., image/png, image/jpeg, image/webp)");
    }

    private static bool BeValidBase64(string? base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
            return false;

        try
        {
            // Remove data URL prefix if present
            var cleanBase64 = base64String.Contains(",") 
                ? base64String.Split(',')[1] 
                : base64String;

            Convert.FromBase64String(cleanBase64);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return false;

        var validMimeTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif" };
        return validMimeTypes.Contains(mimeType.ToLowerInvariant());
    }
}
