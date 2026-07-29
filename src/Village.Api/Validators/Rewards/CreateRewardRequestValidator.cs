using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Rewards;

public class CreateRewardRequestValidator : AbstractValidator<CreateRewardRequest>
{
    public CreateRewardRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PointCost).InclusiveBetween(1, 100000);
    }
}
