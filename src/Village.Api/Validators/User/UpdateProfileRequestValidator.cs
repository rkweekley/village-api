using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.User;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(100)
            .WithMessage("Display name must be 100 characters or fewer.")
            .When(x => x.DisplayName != null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("A valid email address is required.")
            .When(x => x.Email != null);

        RuleFor(x => x)
            .Must(x => x.DisplayName != null || x.Email != null || x.BirthDate != null)
            .WithMessage("At least one field (display name, email, or birth date) must be provided.");
    }
}
