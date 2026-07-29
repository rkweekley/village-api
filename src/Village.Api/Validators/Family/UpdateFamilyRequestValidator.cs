using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Family;

public class UpdateFamilyRequestValidator : AbstractValidator<UpdateFamilyRequest>
{
    public UpdateFamilyRequestValidator()
    {
        When(x => x.Name is not null, () => RuleFor(x => x.Name).MaximumLength(200));
        When(x => x.CurrencyName is not null, () => RuleFor(x => x.CurrencyName).MaximumLength(50));
        When(x => x.Timezone is not null, () => RuleFor(x => x.Timezone).MaximumLength(100));
    }
}
