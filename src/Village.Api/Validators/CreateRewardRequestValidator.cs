using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class CreateRewardRequestValidator : AbstractValidator<CreateRewardRequest>
{
    public CreateRewardRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Reward name is required")
            .MaximumLength(200).WithMessage("Reward name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).When(x => x.Description != null)
            .WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.PointCost)
            .GreaterThanOrEqualTo(1).WithMessage("Point cost must be at least 1")
            .LessThanOrEqualTo(1000000).WithMessage("Point cost must not exceed 1,000,000");

        RuleFor(x => x.MaxRedemptions)
            .GreaterThanOrEqualTo(1).When(x => x.MaxRedemptions.HasValue)
            .WithMessage("Max redemptions must be at least 1")
            .LessThanOrEqualTo(10000).When(x => x.MaxRedemptions.HasValue)
            .WithMessage("Max redemptions must not exceed 10,000");
    }
}
