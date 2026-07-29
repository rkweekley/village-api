using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators;

public class CreateCalendarEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateCalendarEventRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Event title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(4000).When(x => x.Description != null)
            .WithMessage("Description must not exceed 4000 characters");

        RuleFor(x => x.Location)
            .MaximumLength(500).When(x => x.Location != null)
            .WithMessage("Location must not exceed 500 characters");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Start time is required");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("End time is required")
            .GreaterThanOrEqualTo(x => x.StartTime)
            .WithMessage("End time must be on or after start time");
    }
}
