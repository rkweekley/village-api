using FluentValidation;
using Village.Api.Modules;

namespace Village.Api.Validators.Calendar;

public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Location).MaximumLength(500);
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.EndTime > x.StartTime)
            .WithMessage("End time must be after start time.");
    }
}
