using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class AddShoppingItemRequestValidator : AbstractValidator<AddItemRequest>
{
    public AddShoppingItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Item name is required")
            .MaximumLength(200).WithMessage("Item name must not exceed 200 characters");

        RuleFor(x => x.Category)
            .MaximumLength(100).When(x => x.Category != null)
            .WithMessage("Category must not exceed 100 characters");

        RuleFor(x => x.Unit)
            .MaximumLength(50).When(x => x.Unit != null)
            .WithMessage("Unit must not exceed 50 characters");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1).WithMessage("Quantity must be at least 1")
            .LessThanOrEqualTo(9999).WithMessage("Quantity must not exceed 9,999");
    }
}
