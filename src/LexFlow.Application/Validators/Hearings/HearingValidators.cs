using FluentValidation;
using LexFlow.Application.Commands.Hearings;

namespace LexFlow.Application.Validators.Hearings;

public sealed class CreateHearingCommandValidator : AbstractValidator<CreateHearingCommand>
{
    public CreateHearingCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
    }
}

/// <summary>Module 5 Validation: "outcome requires summary >= 10 chars"; Error Handling: "missing next date & not disposed -> force explicit choice {nextDate | sineDie | disposed}".</summary>
public sealed class RecordHearingOutcomeCommandValidator : AbstractValidator<RecordHearingOutcomeCommand>
{
    public RecordHearingOutcomeCommandValidator()
    {
        RuleFor(x => x.HearingId).NotEmpty();
        RuleFor(x => x.Summary).NotEmpty().MinimumLength(10);
        RuleFor(x => x).Must(x => new[] { x.NextHearingDate.HasValue, x.SineDie, x.Disposed }.Count(v => v) == 1)
            .WithMessage("Exactly one of nextHearingDate, sineDie, or disposed must be specified.")
            .WithName("nextHearingDate");
    }
}
