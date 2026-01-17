using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using WebApi.Configuration.Options;

namespace WebApi.Configuration.Validators;

/// <summary>
/// Validates Gemini settings configuration using FluentValidation
/// </summary>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class GeminiOptionsValidator : AbstractValidator<GeminiOptions>
{
    public GeminiOptionsValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Gemini ProjectId is required");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Gemini Location is required");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Gemini Model is required");

        RuleFor(x => x.MaxTokens)
            .GreaterThanOrEqualTo(1).WithMessage("MaxTokens must be at least 1")
            .LessThanOrEqualTo(8192).WithMessage("MaxTokens should not exceed 8192");

        RuleFor(x => x.Temperature)
            .GreaterThanOrEqualTo(0f).WithMessage("Temperature must be at least 0")
            .LessThanOrEqualTo(2f).WithMessage("Temperature should not exceed 2");
    }
}
