using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Shopping;

public class CreateShoppingListRequestValidator : AbstractValidator<CreateShoppingListRequest>
{
    public CreateShoppingListRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
