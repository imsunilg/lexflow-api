using FluentValidation;
using LexFlow.Application.Commands.Billing;

namespace LexFlow.Application.Validators.Billing;

public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.DueInDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => (x.PullTimeEntryIds is { Count: > 0 }) || (x.ExtraLines is { Count: > 0 }))
            .WithMessage("Invoice needs at least one line (Module 8 Validation Rules).");
        RuleForEach(x => x.ExtraLines).ChildRules(line =>
        {
            line.RuleFor(l => l.Qty).GreaterThan(0);
            line.RuleFor(l => l.Rate).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class RejectInvoiceCommandValidator : AbstractValidator<RejectInvoiceCommand>
{
    public RejectInvoiceCommandValidator() => RuleFor(x => x.Reason).NotEmpty();
}

public sealed class VoidInvoiceCommandValidator : AbstractValidator<VoidInvoiceCommand>
{
    public VoidInvoiceCommandValidator() => RuleFor(x => x.Reason).NotEmpty();
}
