using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class UpdateRecipeRequestValidator : AbstractValidator<UpdateRecipeRequest>
{
    public UpdateRecipeRequestValidator()
    {
        RuleFor(x => x.Title)
            .MinimumLength(1).When(x => x.Title != null)
            .WithMessage("Recipe title must not be empty")
            .MaximumLength(200).When(x => x.Title != null)
            .WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(4000).When(x => x.Description != null)
            .WithMessage("Description must not exceed 4000 characters");

        RuleFor(x => x.Ingredients)
            .MinimumLength(1).When(x => x.Ingredients != null)
            .WithMessage("Ingredients must not be empty")
            .MaximumLength(10000).When(x => x.Ingredients != null)
            .WithMessage("Ingredients must not exceed 10,000 characters");

        RuleFor(x => x.Instructions)
            .MinimumLength(1).When(x => x.Instructions != null)
            .WithMessage("Instructions must not be empty")
            .MaximumLength(20000).When(x => x.Instructions != null)
            .WithMessage("Instructions must not exceed 20,000 characters");

        RuleFor(x => x.PrepTimeMinutes)
            .GreaterThanOrEqualTo(1).When(x => x.PrepTimeMinutes.HasValue)
            .WithMessage("Prep time must be at least 1 minute")
            .LessThanOrEqualTo(1440).When(x => x.PrepTimeMinutes.HasValue)
            .WithMessage("Prep time must not exceed 24 hours (1440 minutes)");

        RuleFor(x => x.Servings)
            .GreaterThanOrEqualTo(1).When(x => x.Servings.HasValue)
            .WithMessage("Servings must be at least 1")
            .LessThanOrEqualTo(100).When(x => x.Servings.HasValue)
            .WithMessage("Servings must not exceed 100");
    }
}
