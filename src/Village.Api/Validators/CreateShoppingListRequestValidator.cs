using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class CreateShoppingListRequestValidator : AbstractValidator<CreateShoppingListRequest>
{
    public CreateShoppingListRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Shopping list name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");
    }
}
