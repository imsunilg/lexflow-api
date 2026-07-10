using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Trust;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Trust;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 8 trust (client money) accounting — PRD §17. AC-B4/AC-B7. Trust ledger visible to Finance + matter owner + Owner role per Security Rules (enforced via the trust.read.all permission gate below).</summary>
[ApiController]
[Route("api/v1/trust")]
public sealed class TrustController(IMediator mediator) : ControllerBase
{
    [HttpPost("{clientId:guid}/deposits")]
    [RequirePermission("trust.deposit.all")]
    public async Task<IActionResult> Deposit(Guid clientId, [FromBody] DepositTrustRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TrustLedgerEntryDto>.Of(await mediator.Send(new DepositTrustCommand(clientId, request.Amount, request.Purpose, request.AuthorizationRef), cancellationToken)));

    [HttpPost("{clientId:guid}/disbursements")]
    [RequirePermission("trust.disburse.all")]
    public async Task<IActionResult> Disburse(Guid clientId, [FromBody] DisburseTrustRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TrustLedgerEntryDto>.Of(await mediator.Send(new DisburseTrustCommand(clientId, request.Amount, request.Purpose, request.InvoiceId, request.AuthorizationRef, request.SecondApproverId), cancellationToken)));

    [HttpPost("entries/{ledgerEntryId:guid}/reverse")]
    [RequirePermission("trust.deposit.all")]
    public async Task<IActionResult> Reverse(Guid ledgerEntryId, [FromBody] ReverseTrustEntryRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TrustLedgerEntryDto>.Of(await mediator.Send(new ReverseTrustEntryCommand(ledgerEntryId, request.Reason), cancellationToken)));

    [HttpGet("{clientId:guid}/ledger")]
    [RequirePermission("trust.read.all")]
    public async Task<IActionResult> GetLedger(Guid clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TrustLedgerEntryDto>>.Of(await mediator.Send(new GetTrustLedgerQuery(clientId), cancellationToken)));

    [HttpGet("{clientId:guid}")]
    [RequirePermission("trust.read.all")]
    public async Task<IActionResult> GetAccount(Guid clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<TrustAccountDto>.Of(await mediator.Send(new GetTrustAccountQuery(clientId), cancellationToken)));

    [HttpPost("reconciliations")]
    [RequirePermission("trust.read.all")]
    public async Task<IActionResult> ImportReconciliation([FromBody] ImportTrustReconciliationRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TrustReconciliationDto>.Of(await mediator.Send(new ImportTrustReconciliationCommand(request.PeriodStart, request.PeriodEnd, request.BankStatementBalance, request.Lines, request.ImportedCsvBlobPath), cancellationToken)));

    [HttpPost("reconciliations/{id:guid}/signoff")]
    [RequirePermission("trust.read.all")]
    public async Task<IActionResult> SignOffReconciliation(Guid id, [FromBody] SignOffReconciliationRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<TrustReconciliationDto>.Of(await mediator.Send(new SignOffTrustReconciliationCommand(id, request.Notes), cancellationToken)));

    [HttpGet("reconciliations/{id:guid}/exceptions")]
    [RequirePermission("trust.read.all")]
    public async Task<IActionResult> GetExceptions(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<TrustReconciliationItemDto>>.Of(await mediator.Send(new GetTrustReconciliationExceptionsQuery(id), cancellationToken)));
}

public sealed record DepositTrustRequest(decimal Amount, string? Purpose, string? AuthorizationRef);

public sealed record DisburseTrustRequest(decimal Amount, string? Purpose, Guid? InvoiceId, string AuthorizationRef, Guid? SecondApproverId);

public sealed record ReverseTrustEntryRequest(string Reason);

public sealed record ImportTrustReconciliationRequest(DateOnly PeriodStart, DateOnly PeriodEnd, decimal BankStatementBalance, IReadOnlyList<BankStatementLineInput> Lines, string? ImportedCsvBlobPath);

public sealed record SignOffReconciliationRequest(string? Notes);
