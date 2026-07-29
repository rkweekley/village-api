using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Chores;

public class CreateChoreRequestValidator : AbstractValidator<CreateChoreRequest>
{
    public CreateChoreRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PointValue).InclusiveBetween(1, 10000);
    }
}
