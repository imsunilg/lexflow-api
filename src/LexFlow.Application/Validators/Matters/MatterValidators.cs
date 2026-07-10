using FluentValidation;
using LexFlow.Application.Commands.Matters;

namespace LexFlow.Application.Validators.Matters;

public sealed class CreateMatterCommandValidator : AbstractValidator<CreateMatterCommand>
{
    private static readonly string[] MatterTypes = ["Litigation", "Advisory", "Transactional", "Arbitration", "Compliance"];
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Critical"];

    public CreateMatterCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(3, 300);
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.MatterType).NotEmpty().Must(t => MatterTypes.Contains(t)).WithMessage($"MatterType must be one of {string.Join('|', MatterTypes)}.");
        RuleFor(x => x.ResponsibleLawyerId).NotEmpty();
        RuleFor(x => x.Priority).NotEmpty().Must(p => Priorities.Contains(p)).WithMessage($"Priority must be one of {string.Join('|', Priorities)}.");
        RuleFor(x => x.OpenedOn).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Open date must not be in the future.");
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue);
        RuleFor(x => x.ConflictOverrideReason).NotEmpty().When(x => x.OverrideConflict)
            .WithMessage("A reason is required when overriding a conflict-of-interest hit (AC-M1).");
    }
}

public sealed class UpdateMatterCommandValidator : AbstractValidator<UpdateMatterCommand>
{
    public UpdateMatterCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().Length(3, 300);
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue);
    }
}

public sealed class ChangeMatterStatusCommandValidator : AbstractValidator<ChangeMatterStatusCommand>
{
    private static readonly string[] Statuses = ["Open", "OnHold", "Closed", "Reopened"];

    public ChangeMatterStatusCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.ToStatus).NotEmpty().Must(s => Statuses.Contains(s)).WithMessage($"ToStatus must be one of {string.Join('|', Statuses)}.");
        RuleFor(x => x.Outcome).Must(o => new[] { "Won", "Lost", "Settled", "Withdrawn" }.Contains(o)).When(x => x.Outcome is not null);
    }
}

public sealed class AddMatterPartyCommandValidator : AbstractValidator<AddMatterPartyCommand>
{
    private static readonly string[] Roles = ["Client", "Opposite", "Co-party", "Witness-org"];

    public AddMatterPartyCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.PartyRole).NotEmpty().Must(r => Roles.Contains(r)).WithMessage($"PartyRole must be one of {string.Join('|', Roles)}.");
    }
}

public sealed class AddMatterImportantDateCommandValidator : AbstractValidator<AddMatterImportantDateCommand>
{
    private static readonly string[] Kinds = ["Limitation", "Filing", "Compliance", "Custom"];

    public AddMatterImportantDateCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().Must(k => Kinds.Contains(k)).WithMessage($"Kind must be one of {string.Join('|', Kinds)}.");
        RuleFor(x => x.Title).NotEmpty();
    }
}

public sealed class AddMatterExpenseCommandValidator : AbstractValidator<AddMatterExpenseCommand>
{
    public AddMatterExpenseCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IncurredOn).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Expense date must not be in the future.");
    }
}

public sealed class AddMatterRelatedCommandValidator : AbstractValidator<AddMatterRelatedCommand>
{
    private static readonly string[] RelationTypes = ["Appeal-of", "Connected", "Cross-suit"];

    public AddMatterRelatedCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.RelatedMatterId).NotEmpty();
        RuleFor(x => x.RelationType).NotEmpty().Must(t => RelationTypes.Contains(t)).WithMessage($"RelationType must be one of {string.Join('|', RelationTypes)}.");
    }
}
