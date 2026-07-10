using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Billing;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Billing;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 8 invoice lifecycle + batch billing engine — PRD §17.</summary>
[ApiController]
[Route("api/v1/invoices")]
public sealed class InvoicesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("invoices.create.own")]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new CreateInvoiceCommand(request.MatterId, request.IssueDate, request.DueInDays, request.PullTimeEntryIds, request.ExtraLines, request.Discount, request.Notes), cancellationToken)));

    [HttpPost("batch")]
    [RequirePermission("invoices.create.all")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchInvoicesRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<InvoiceDto>>.Of(await mediator.Send(new CreateBatchInvoicesCommand(request.MinWip, request.BranchId, request.MatterTypeId, request.AsOf), cancellationToken)));

    [HttpGet]
    [RequirePermission("invoices.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] Guid? clientId, [FromQuery] Guid? matterId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] bool? overdueOnly, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<InvoiceDto>>.Of(await mediator.Send(new GetInvoicesQuery(status, clientId, matterId, from, to, overdueOnly), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("invoices.read.own")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto?>.Of(await mediator.Send(new GetInvoiceQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("invoices.update.own")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new UpdateInvoiceCommand(id, request.IssueDate, request.DueDate, request.Notes), cancellationToken)));

    [HttpPost("{id:guid}/submit")]
    [RequirePermission("invoices.update.own")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new SubmitInvoiceCommand(id), cancellationToken)));

    [HttpPost("{id:guid}/approve")]
    [RequirePermission("invoices.approve.team")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new ApproveInvoiceCommand(id), cancellationToken)));

    [HttpPost("{id:guid}/reject")]
    [RequirePermission("invoices.approve.team")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectInvoiceRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new RejectInvoiceCommand(id, request.Reason), cancellationToken)));

    [HttpPost("{id:guid}/send")]
    [RequirePermission("invoices.send.all")]
    public async Task<IActionResult> Send(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new SendInvoiceCommand(id), cancellationToken)));

    [HttpPost("{id:guid}/void")]
    [RequirePermission("invoices.void.all")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidInvoiceRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<InvoiceDto>.Of(await mediator.Send(new VoidInvoiceCommand(id, request.Reason), cancellationToken)));

    [HttpGet("{id:guid}/pdf")]
    [RequirePermission("invoices.read.own")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
        => File(await mediator.Send(new GetInvoicePdfQuery(id), cancellationToken), "application/pdf", $"invoice-{id}.pdf");

    [HttpGet("{id:guid}/status-history")]
    [RequirePermission("invoices.read.own")]
    public async Task<IActionResult> GetStatusHistory(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<InvoiceStatusHistoryDto>>.Of(await mediator.Send(new GetInvoiceStatusHistoryQuery(id), cancellationToken)));
}

public sealed record CreateInvoiceRequest(Guid MatterId, DateOnly? IssueDate, int DueInDays, IReadOnlyList<Guid>? PullTimeEntryIds, IReadOnlyList<ExtraLineInput>? ExtraLines, DiscountInput? Discount, string? Notes);

public sealed record CreateBatchInvoicesRequest(decimal MinWip, Guid? BranchId, Guid? MatterTypeId, DateOnly AsOf);

public sealed record UpdateInvoiceRequest(DateOnly? IssueDate, DateOnly? DueDate, string? Notes);

public sealed record RejectInvoiceRequest(string Reason);

public sealed record VoidInvoiceRequest(string Reason);
