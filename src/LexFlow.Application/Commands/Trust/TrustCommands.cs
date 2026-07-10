using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Trust;

public sealed record DepositTrustCommand(Guid ClientId, decimal Amount, string? Purpose, string? AuthorizationRef) : IRequest<TrustLedgerEntryDto>;

public sealed class DepositTrustCommandHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<DepositTrustCommand, TrustLedgerEntryDto>
{
    public Task<TrustLedgerEntryDto> Handle(DepositTrustCommand request, CancellationToken cancellationToken)
        => service.DepositAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.ClientId, request.Amount, request.Purpose, request.AuthorizationRef, cancellationToken);
}

public sealed record DisburseTrustCommand(Guid ClientId, decimal Amount, string? Purpose, Guid? InvoiceId, string AuthorizationRef, Guid? SecondApproverId) : IRequest<TrustLedgerEntryDto>;

public sealed class DisburseTrustCommandHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<DisburseTrustCommand, TrustLedgerEntryDto>
{
    public Task<TrustLedgerEntryDto> Handle(DisburseTrustCommand request, CancellationToken cancellationToken)
        => service.DisburseAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.ClientId, request.Amount, request.Purpose, request.InvoiceId, request.AuthorizationRef, request.SecondApproverId, cancellationToken);
}

public sealed record ReverseTrustEntryCommand(Guid LedgerEntryId, string Reason) : IRequest<TrustLedgerEntryDto>;

public sealed class ReverseTrustEntryCommandHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<ReverseTrustEntryCommand, TrustLedgerEntryDto>
{
    public Task<TrustLedgerEntryDto> Handle(ReverseTrustEntryCommand request, CancellationToken cancellationToken)
        => service.ReverseAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.LedgerEntryId, request.Reason, cancellationToken);
}

public sealed record ImportTrustReconciliationCommand(DateOnly PeriodStart, DateOnly PeriodEnd, decimal BankStatementBalance, IReadOnlyList<BankStatementLineInput> Lines, string? ImportedCsvBlobPath) : IRequest<TrustReconciliationDto>;

public sealed class ImportTrustReconciliationCommandHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<ImportTrustReconciliationCommand, TrustReconciliationDto>
{
    public Task<TrustReconciliationDto> Handle(ImportTrustReconciliationCommand request, CancellationToken cancellationToken)
        => service.ImportReconciliationAsync(currentUser.TenantId!.Value, request.PeriodStart, request.PeriodEnd, request.BankStatementBalance, request.Lines, request.ImportedCsvBlobPath, cancellationToken);
}

public sealed record SignOffTrustReconciliationCommand(Guid Id, string? Notes) : IRequest<TrustReconciliationDto>;

public sealed class SignOffTrustReconciliationCommandHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<SignOffTrustReconciliationCommand, TrustReconciliationDto>
{
    public Task<TrustReconciliationDto> Handle(SignOffTrustReconciliationCommand request, CancellationToken cancellationToken)
        => service.SignOffReconciliationAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Id, request.Notes, cancellationToken);
}
