using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class CreateRecipeRequestValidator : AbstractValidator<CreateRecipeRequest>
{
    public CreateRecipeRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Recipe title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(4000).When(x => x.Description != null)
            .WithMessage("Description must not exceed 4000 characters");

        RuleFor(x => x.Ingredients)
            .NotEmpty().WithMessage("Ingredients are required")
            .MaximumLength(10000).WithMessage("Ingredients must not exceed 10,000 characters");

        RuleFor(x => x.Instructions)
            .NotEmpty().WithMessage("Instructions are required")
            .MaximumLength(20000).WithMessage("Instructions must not exceed 20,000 characters");

        RuleFor(x => x.PrepTimeMinutes)
            .GreaterThanOrEqualTo(1).WithMessage("Prep time must be at least 1 minute")
            .LessThanOrEqualTo(1440).WithMessage("Prep time must not exceed 24 hours (1440 minutes)");

        RuleFor(x => x.Servings)
            .GreaterThanOrEqualTo(1).WithMessage("Servings must be at least 1")
            .LessThanOrEqualTo(100).WithMessage("Servings must not exceed 100");
    }
}
