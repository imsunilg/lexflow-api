using FluentValidation;
using LexFlow.Application.Commands.Kb;

namespace LexFlow.Application.Validators.Kb;

public sealed class CreateKbActCommandValidator : AbstractValidator<CreateKbActCommand>
{
    public CreateKbActCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
}

public sealed class CreateKbActSectionCommandValidator : AbstractValidator<CreateKbActSectionCommand>
{
    public CreateKbActSectionCommandValidator()
    {
        RuleFor(x => x.ActId).NotEmpty();
        RuleFor(x => x.Number).NotEmpty().MaximumLength(50);
    }
}

public sealed class AmendKbActSectionCommandValidator : AbstractValidator<AmendKbActSectionCommand>
{
    public AmendKbActSectionCommandValidator() => RuleFor(x => x.SectionId).NotEmpty();
}

public sealed class UploadKbJudgmentCommandValidator : AbstractValidator<UploadKbJudgmentCommand>
{
    public UploadKbJudgmentCommandValidator()
    {
        RuleFor(x => x.Citation).NotEmpty();
        RuleFor(x => x.FileContent).Must(f => f.LongLength <= 50 * 1024 * 1024).WithMessage("Judgment PDF must not exceed 50 MB (Module 12 Validation Rules).");
    }
}

public sealed class CreateKbArticleDraftCommandValidator : AbstractValidator<CreateKbArticleDraftCommand>
{
    public CreateKbArticleDraftCommandValidator() => RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
}

public sealed class UpdateKbArticleDraftCommandValidator : AbstractValidator<UpdateKbArticleDraftCommand>
{
    public UpdateKbArticleDraftCommandValidator() => RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
}

public sealed class AssignKbArticleReviewerCommandValidator : AbstractValidator<AssignKbArticleReviewerCommand>
{
    public AssignKbArticleReviewerCommandValidator() => RuleFor(x => x.ReviewerId).NotEmpty();
}

public sealed class PinKbItemToMatterCommandValidator : AbstractValidator<PinKbItemToMatterCommand>
{
    public PinKbItemToMatterCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.KbRefKind).NotEmpty().Must(k => new[] { "Act", "ActSection", "Judgment", "Article", "Template" }.Contains(k));
        RuleFor(x => x.KbRefId).NotEmpty();
    }
}
