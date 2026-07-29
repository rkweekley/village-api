using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Shopping;

public class AddItemRequestValidator : AbstractValidator<AddItemRequest>
{
    public AddItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 99999);
    }
}
