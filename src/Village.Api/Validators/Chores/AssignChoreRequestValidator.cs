using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Chores;

public class AssignChoreRequestValidator : AbstractValidator<AssignChoreRequest>
{
    public AssignChoreRequestValidator()
    {
        RuleFor(x => x.AssignedToId).NotEmpty();
    }
}
