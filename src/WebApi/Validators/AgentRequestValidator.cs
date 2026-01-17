using FluentValidation;
using Web.Common.DTOs.Agent;

namespace WebApi.Validators;

public class AgentRequestValidator : AbstractValidator<AgentRequest>
{
    public AgentRequestValidator()
    {
        RuleFor(x => x.UserGoal)
            .NotEmpty().WithMessage("UserGoal is required")
            .MaximumLength(1000).WithMessage("UserGoal must not exceed 1000 characters");

        RuleFor(x => x.PageState)
            .NotNull().WithMessage("PageState is required");

        RuleFor(x => x.PageState.Url)
            .NotEmpty().WithMessage("PageState.Url is required")
            .MaximumLength(2000).WithMessage("PageState.Url must not exceed 2000 characters")
            .Must(BeValidUrl).WithMessage("PageState.Url must be a valid URL");

        RuleFor(x => x.PageState.Html)
            .NotEmpty().WithMessage("PageState.Html is required");
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
