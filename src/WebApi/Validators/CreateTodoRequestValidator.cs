using FluentValidation;
using WebApi.DTOs.Todos;
using WebApi.Models;

namespace WebApi.Validators;

public class CreateTodoRequestValidator 
    : AbstractValidator<CreateTodoRequest>
{
    public CreateTodoRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Priority must be a valid value (Low, Medium, or High)");

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Due date must be in the future")
            .When(x => x.DueDate.HasValue);

        RuleFor(x => x.TagIds)
            .Must(BeValidGuids).WithMessage("All tag IDs must be valid GUIDs")
            .When(x => x.TagIds != null && x.TagIds.Any());
    }

    private static bool BeValidGuids(List<Guid>? tagIds) 
        => tagIds == null || tagIds.All(id => id != Guid.Empty);
}
