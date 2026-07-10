using FluentValidation;
using LexFlow.Application.Commands.Cases;

namespace LexFlow.Application.Validators.Cases;

public sealed class CreateCourtCaseCommandValidator : AbstractValidator<CreateCourtCaseCommand>
{
    public CreateCourtCaseCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.CourtId).NotEmpty();
        RuleFor(x => x.CaseType).NotEmpty();
        RuleFor(x => x.CaseNumber).NotEmpty();
        RuleFor(x => x.CaseYear).InclusiveBetween(1900, 2100);
    }
}

public sealed class ChangeCourtCaseStageCommandValidator : AbstractValidator<ChangeCourtCaseStageCommand>
{
    public ChangeCourtCaseStageCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.ToStage).NotEmpty();
    }
}

public sealed class FileCourtCaseAppealCommandValidator : AbstractValidator<FileCourtCaseAppealCommand>
{
    public FileCourtCaseAppealCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.TargetCourtId).NotEmpty();
    }
}

public sealed class AddCasePartyCommandValidator : AbstractValidator<AddCasePartyCommand>
{
    public AddCasePartyCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.PartyRole).NotEmpty();
    }
}

public sealed class AddCourtOrderCommandValidator : AbstractValidator<AddCourtOrderCommand>
{
    public AddCourtOrderCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.OrderDate).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Order date must not be in the future.");
    }
}

public sealed class AddEvidenceItemCommandValidator : AbstractValidator<AddEvidenceItemCommand>
{
    private static readonly string[] Kinds = ["Documentary", "Electronic", "Physical"];

    public AddEvidenceItemCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().Must(k => Kinds.Contains(k)).WithMessage($"Kind must be one of {string.Join('|', Kinds)}.");
    }
}

public sealed class AddWitnessCommandValidator : AbstractValidator<AddWitnessCommand>
{
    public AddWitnessCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class AddArgumentNoteCommandValidator : AbstractValidator<AddArgumentNoteCommand>
{
    public AddArgumentNoteCommandValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty();
    }
}
