using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Payments;

public sealed record RecordPaymentCommand(Guid ClientId, decimal Amount, string Mode, string? Gateway, string? GatewayRef, DateOnly ReceivedOn, IReadOnlyList<PaymentAllocationInput> Allocations, string? IdempotencyKey) : IRequest<PaymentDto>;

public sealed class RecordPaymentCommandHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<RecordPaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
        => service.RecordPaymentAsync(currentUser.TenantId!.Value, new RecordPaymentInput(request.ClientId, request.Amount, request.Mode, request.Gateway, request.GatewayRef, request.ReceivedOn, request.Allocations, request.IdempotencyKey), cancellationToken);
}

public sealed record CreateCreditNoteCommand(Guid InvoiceId, decimal Amount, string Reason) : IRequest<CreditNoteDto>;

public sealed class CreateCreditNoteCommandHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<CreateCreditNoteCommand, CreditNoteDto>
{
    public Task<CreditNoteDto> Handle(CreateCreditNoteCommand request, CancellationToken cancellationToken)
        => service.CreateCreditNoteAsync(currentUser.TenantId!.Value, request.InvoiceId, request.Amount, request.Reason, cancellationToken);
}

public sealed record ApplyCreditNoteCommand(Guid Id) : IRequest<CreditNoteDto>;

public sealed class ApplyCreditNoteCommandHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<ApplyCreditNoteCommand, CreditNoteDto>
{
    public Task<CreditNoteDto> Handle(ApplyCreditNoteCommand request, CancellationToken cancellationToken)
        => service.ApplyCreditNoteAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record CreateRefundCommand(Guid PaymentId, decimal Amount, string? Reason) : IRequest<RefundDto>;

public sealed class CreateRefundCommandHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<CreateRefundCommand, RefundDto>
{
    public Task<RefundDto> Handle(CreateRefundCommand request, CancellationToken cancellationToken)
        => service.CreateRefundAsync(currentUser.TenantId!.Value, request.PaymentId, request.Amount, request.Reason, cancellationToken);
}
