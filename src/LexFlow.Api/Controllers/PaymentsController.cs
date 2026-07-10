using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Payments;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Payments;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 8 payments, credit notes, refunds, client statement — PRD §17. AC-B3.</summary>
[ApiController]
[Route("api/v1")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
    /// <summary>AC-B3: idempotent by Idempotency-Key header (also accepted in-body for callers that can't set custom headers).</summary>
    [HttpPost("payments")]
    [RequirePermission("payments.record.all")]
    public async Task<IActionResult> Record([FromBody] RecordPaymentRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader, CancellationToken cancellationToken)
        => Ok(ApiResponse<PaymentDto>.Of(await mediator.Send(new RecordPaymentCommand(request.ClientId, request.Amount, request.Mode, request.Gateway, request.GatewayRef, request.ReceivedOn, request.Allocations, idempotencyKeyHeader ?? request.IdempotencyKey), cancellationToken)));

    [HttpGet("payments/{id:guid}")]
    [RequirePermission("payments.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<PaymentDto?>.Of(await mediator.Send(new GetPaymentQuery(id), cancellationToken)));

    [HttpGet("clients/{clientId:guid}/payments")]
    [RequirePermission("payments.read.all")]
    public async Task<IActionResult> GetForClient(Guid clientId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<PaymentDto>>.Of(await mediator.Send(new GetPaymentsForClientQuery(clientId), cancellationToken)));

    [HttpGet("clients/{clientId:guid}/statement")]
    [RequirePermission("invoices.read.all")]
    public async Task<IActionResult> GetStatement(Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientStatementDto>.Of(await mediator.Send(new GetClientStatementQuery(clientId, from, to), cancellationToken)));

    [HttpPost("credit-notes")]
    [RequirePermission("invoices.update.all")]
    public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CreditNoteDto>.Of(await mediator.Send(new CreateCreditNoteCommand(request.InvoiceId, request.Amount, request.Reason), cancellationToken)));

    [HttpPost("credit-notes/{id:guid}/apply")]
    [RequirePermission("invoices.update.all")]
    public async Task<IActionResult> ApplyCreditNote(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<CreditNoteDto>.Of(await mediator.Send(new ApplyCreditNoteCommand(id), cancellationToken)));

    [HttpPost("refunds")]
    [RequirePermission("payments.refund.all")]
    public async Task<IActionResult> CreateRefund([FromBody] CreateRefundRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RefundDto>.Of(await mediator.Send(new CreateRefundCommand(request.PaymentId, request.Amount, request.Reason), cancellationToken)));
}

public sealed record RecordPaymentRequest(Guid ClientId, decimal Amount, string Mode, string? Gateway, string? GatewayRef, DateOnly ReceivedOn, IReadOnlyList<PaymentAllocationInput> Allocations, string? IdempotencyKey);

public sealed record CreateCreditNoteRequest(Guid InvoiceId, decimal Amount, string Reason);

public sealed record CreateRefundRequest(Guid PaymentId, decimal Amount, string? Reason);
