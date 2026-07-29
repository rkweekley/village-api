using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class CreateChoreRequestValidator : AbstractValidator<CreateChoreRequest>
{
    public CreateChoreRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Chore name is required")
            .MaximumLength(200).WithMessage("Chore name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).When(x => x.Description != null)
            .WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.PointValue)
            .GreaterThanOrEqualTo(1).WithMessage("Point value must be at least 1")
            .LessThanOrEqualTo(10000).WithMessage("Point value must not exceed 10,000");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be non-negative");
    }
}
