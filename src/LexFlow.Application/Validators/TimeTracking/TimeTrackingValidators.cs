using FluentValidation;
using LexFlow.Application.Commands.TimeTracking;

namespace LexFlow.Application.Validators.TimeTracking;

public sealed class CreateTimeEntryCommandValidator : AbstractValidator<CreateTimeEntryCommand>
{
    public CreateTimeEntryCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.DurationMin).InclusiveBetween(1, 1440);
        RuleFor(x => x.EntryDate).LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Time entries cannot be dated in the future.");
        RuleFor(x => x.Narrative).Length(5, 2000).When(x => x.Billable).WithMessage("A billable entry requires a 5-2000 character narrative.");
    }
}

public sealed class UpdateTimeEntryCommandValidator : AbstractValidator<UpdateTimeEntryCommand>
{
    public UpdateTimeEntryCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.DurationMin).InclusiveBetween(1, 1440);
        RuleFor(x => x.Narrative).Length(5, 2000).When(x => x.Billable).WithMessage("A billable entry requires a 5-2000 character narrative.");
    }
}

public sealed class SubmitTimeEntriesCommandValidator : AbstractValidator<SubmitTimeEntriesCommand>
{
    public SubmitTimeEntriesCommandValidator() => RuleFor(x => x.Ids).NotEmpty();
}

public sealed class ApproveTimeEntriesCommandValidator : AbstractValidator<ApproveTimeEntriesCommand>
{
    public ApproveTimeEntriesCommandValidator() => RuleFor(x => x.Ids).NotEmpty();
}

public sealed class RejectTimeEntriesCommandValidator : AbstractValidator<RejectTimeEntriesCommand>
{
    public RejectTimeEntriesCommandValidator() => RuleFor(x => x.Ids).NotEmpty();
}
