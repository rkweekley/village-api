using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Meals;

public class UpdateRecipeRequestValidator : AbstractValidator<UpdateRecipeRequest>
{
    public UpdateRecipeRequestValidator()
    {
        When(x => x.Title is not null, () => RuleFor(x => x.Title).MaximumLength(200));
        When(x => x.Description is not null, () => RuleFor(x => x.Description).MaximumLength(2000));
        When(x => x.Ingredients is not null, () => RuleFor(x => x.Ingredients).MaximumLength(10000));
        When(x => x.Instructions is not null, () => RuleFor(x => x.Instructions).MaximumLength(20000));
        When(x => x.PrepTimeMinutes is not null, () => RuleFor(x => x.PrepTimeMinutes).InclusiveBetween(1, 1440));
        When(x => x.Servings is not null, () => RuleFor(x => x.Servings).InclusiveBetween(1, 100));
    }
}
