using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class UpdateChoreRequestValidator : AbstractValidator<UpdateChoreRequest>
{
    public UpdateChoreRequestValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(1).When(x => x.Name != null)
            .WithMessage("Chore name must not be empty")
            .MaximumLength(200).When(x => x.Name != null)
            .WithMessage("Chore name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).When(x => x.Description != null)
            .WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.PointValue)
            .GreaterThanOrEqualTo(1).When(x => x.PointValue.HasValue)
            .WithMessage("Point value must be at least 1")
            .LessThanOrEqualTo(10000).When(x => x.PointValue.HasValue)
            .WithMessage("Point value must not exceed 10,000");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue)
            .WithMessage("Sort order must be non-negative");
    }
}
