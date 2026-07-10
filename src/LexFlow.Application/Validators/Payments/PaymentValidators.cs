using FluentValidation;
using LexFlow.Application.Commands.Payments;

namespace LexFlow.Application.Validators.Payments;

public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Mode).NotEmpty();
        RuleForEach(x => x.Allocations).ChildRules(a => a.RuleFor(x => x.Amount).GreaterThan(0));
    }
}

public sealed class CreateCreditNoteCommandValidator : AbstractValidator<CreateCreditNoteCommand>
{
    public CreateCreditNoteCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public sealed class CreateRefundCommandValidator : AbstractValidator<CreateRefundCommand>
{
    public CreateRefundCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
