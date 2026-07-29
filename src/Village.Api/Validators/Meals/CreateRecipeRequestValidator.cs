using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Meals;

public class CreateRecipeRequestValidator : AbstractValidator<CreateRecipeRequest>
{
    public CreateRecipeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Ingredients).NotEmpty().MaximumLength(10000);
        RuleFor(x => x.Instructions).NotEmpty().MaximumLength(20000);
        RuleFor(x => x.PrepTimeMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.Servings).InclusiveBetween(1, 100);
    }
}
