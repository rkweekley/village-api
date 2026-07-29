using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Chores;

public class UpdateChoreRequestValidator : AbstractValidator<UpdateChoreRequest>
{
    public UpdateChoreRequestValidator()
    {
        When(x => x.Name is not null, () => RuleFor(x => x.Name).MaximumLength(200));
        When(x => x.Description is not null, () => RuleFor(x => x.Description).MaximumLength(2000));
        When(x => x.PointValue is not null, () => RuleFor(x => x.PointValue).InclusiveBetween(1, 10000));
    }
}
