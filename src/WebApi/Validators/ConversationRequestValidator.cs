using FluentValidation;
using Web.Common.DTOs.Agent;

namespace WebApi.Validators;

/// <summary>
/// Validates ConversationRequest using FluentValidation
/// </summary>
public class ConversationRequestValidator : AbstractValidator<ConversationRequest>
{
    public ConversationRequestValidator()
    {
        // Title is required only when creating a new conversation (SessionId is null)
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required when creating a new conversation")
            .MaximumLength(1000).WithMessage("Title must not exceed 1000 characters")
            .When(x => !x.SessionId.HasValue);

        RuleFor(x => x.PageState)
            .NotNull().WithMessage("PageState is required");

        RuleFor(x => x.PageState.Url)
            .NotEmpty().WithMessage("PageState.Url is required")
            .MaximumLength(2000).WithMessage("PageState.Url must not exceed 2000 characters")
            .Must(BeValidUrl).WithMessage("PageState.Url must be a valid URL")
            .When(x => x.PageState != null);

        RuleFor(x => x.PageState.Html)
            .NotEmpty().WithMessage("PageState.Html is required")
            .When(x => x.PageState != null);
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
