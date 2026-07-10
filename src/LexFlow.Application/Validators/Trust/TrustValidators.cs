using FluentValidation;
using LexFlow.Application.Commands.Trust;

namespace LexFlow.Application.Validators.Trust;

public sealed class DepositTrustCommandValidator : AbstractValidator<DepositTrustCommand>
{
    public DepositTrustCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class DisburseTrustCommandValidator : AbstractValidator<DisburseTrustCommand>
{
    public DisburseTrustCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.AuthorizationRef).NotEmpty();
    }
}

public sealed class ReverseTrustEntryCommandValidator : AbstractValidator<ReverseTrustEntryCommand>
{
    public ReverseTrustEntryCommandValidator()
    {
        RuleFor(x => x.LedgerEntryId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}
