using FluentValidation;
using LexFlow.Application.Commands.Leads;

namespace LexFlow.Application.Validators.Leads;

public sealed class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 200);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneE164))
            .WithMessage("At least one of phone/email is required.")
            .WithName("Email");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneE164).Matches(@"^\+[1-9]\d{6,14}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneE164));
    }
}

public sealed class UpdateLeadCommandValidator : AbstractValidator<UpdateLeadCommand>
{
    public UpdateLeadCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneE164).Matches(@"^\+[1-9]\d{6,14}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneE164));
    }
}

public sealed class ChangeLeadStageCommandValidator : AbstractValidator<ChangeLeadStageCommand>
{
    public ChangeLeadStageCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.ToStage).NotEmpty();
    }
}

public sealed class AddLeadActivityCommandValidator : AbstractValidator<AddLeadActivityCommand>
{
    public AddLeadActivityCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.ActivityType).NotEmpty().Must(t => new[] { "call", "email", "meeting", "note" }.Contains(t))
            .WithMessage("ActivityType must be one of call|email|meeting|note.");
    }
}

public sealed class AssignLeadCommandValidator : AbstractValidator<AssignLeadCommand>
{
    public AssignLeadCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x).Must(x => x.UserId.HasValue || x.RuleId.HasValue).WithMessage("Either userId or ruleId is required.").WithName("UserId");
    }
}

public sealed class ConvertLeadCommandValidator : AbstractValidator<ConvertLeadCommand>
{
    public ConvertLeadCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
    }
}

public sealed class MarkLeadLostCommandValidator : AbstractValidator<MarkLeadLostCommand>
{
    public MarkLeadLostCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.ReasonId).NotEmpty();
    }
}

public sealed class ImportLeadsCommandValidator : AbstractValidator<ImportLeadsCommand>
{
    public ImportLeadsCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileContent).NotEmpty();
    }
}

public sealed class CaptureWebToLeadCommandValidator : AbstractValidator<CaptureWebToLeadCommand>
{
    public CaptureWebToLeadCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 200);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneE164))
            .WithMessage("At least one of phone/email is required.")
            .WithName("Email");
    }
}
