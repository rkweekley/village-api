using FluentValidation;
using Village.Api.Dtos.Auth;

namespace Village.Api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MinimumLength(3).WithMessage("Display name must be at least 3 characters")
            .MaximumLength(100).WithMessage("Display name must not exceed 100 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters");

        RuleFor(x => x.InviteCode)
            .MaximumLength(20).When(x => x.InviteCode != null)
            .WithMessage("Invite code must not exceed 20 characters");
    }
}
