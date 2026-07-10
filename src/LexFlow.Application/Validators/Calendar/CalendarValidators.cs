using FluentValidation;
using LexFlow.Application.Commands.Calendar;

namespace LexFlow.Application.Validators.Calendar;

public sealed class CreateCalendarEventCommandValidator : AbstractValidator<CreateCalendarEventCommand>
{
    public CreateCalendarEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(3, 300);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("End must be after start.");
        RuleFor(x => x).Must(x => x.EndsAt - x.StartsAt <= TimeSpan.FromDays(14)).WithMessage("Event duration cannot exceed 14 days.");
        RuleFor(x => x.Attendees).Must(a => a is null || a.Count <= 100).WithMessage("At most 100 attendees.");
    }
}

public sealed class UpdateCalendarEventCommandValidator : AbstractValidator<UpdateCalendarEventCommand>
{
    public UpdateCalendarEventCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().Length(3, 300);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("End must be after start.");
        RuleFor(x => x.Scope).Must(s => s is "occurrence" or "series").WithMessage("Scope must be 'occurrence' or 'series'.");
        RuleFor(x => x.OccurrenceDate).NotNull().When(x => x.Scope == "occurrence").WithMessage("occurrenceDate is required when scope=occurrence.");
    }
}

public sealed class AddEventReminderCommandValidator : AbstractValidator<AddEventReminderCommand>
{
    private static readonly string[] Channels = ["Email", "SMS", "WhatsApp", "Push", "InApp"];

    public AddEventReminderCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.OffsetMinutes).InclusiveBetween(0, 129_600).WithMessage("Reminder offsets must be between 0 and 90 days.");
        RuleFor(x => x.Channel).Must(c => Channels.Contains(c)).WithMessage($"Channel must be one of {string.Join('|', Channels)}.");
    }
}

public sealed class ConnectExternalCalendarCommandValidator : AbstractValidator<ConnectExternalCalendarCommand>
{
    public ConnectExternalCalendarCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty();
        RuleFor(x => x.RedirectUri).NotEmpty();
    }
}
