using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Billing;

public sealed record CreateInvoiceCommand(Guid MatterId, DateOnly? IssueDate, int DueInDays, IReadOnlyList<Guid>? PullTimeEntryIds, IReadOnlyList<ExtraLineInput>? ExtraLines, DiscountInput? Discount, string? Notes) : IRequest<InvoiceDto>;

public sealed class CreateInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        => service.CreateDraftAsync(currentUser.TenantId!.Value, request.MatterId, new CreateInvoiceInput(request.IssueDate, request.DueInDays, request.PullTimeEntryIds, request.ExtraLines, request.Discount, request.Notes), cancellationToken);
}

public sealed record CreateBatchInvoicesCommand(decimal MinWip, Guid? BranchId, Guid? MatterTypeId, DateOnly AsOf) : IRequest<IReadOnlyList<InvoiceDto>>;

public sealed class CreateBatchInvoicesCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<CreateBatchInvoicesCommand, IReadOnlyList<InvoiceDto>>
{
    public Task<IReadOnlyList<InvoiceDto>> Handle(CreateBatchInvoicesCommand request, CancellationToken cancellationToken)
        => service.CreateBatchAsync(currentUser.TenantId!.Value, new BatchBillingFilter(request.MinWip, request.BranchId, request.MatterTypeId), request.AsOf, cancellationToken);
}

public sealed record UpdateInvoiceCommand(Guid Id, DateOnly? IssueDate, DateOnly? DueDate, string? Notes) : IRequest<InvoiceDto>;

public sealed class UpdateInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<UpdateInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(UpdateInvoiceCommand request, CancellationToken cancellationToken)
        => service.UpdateDraftAsync(currentUser.TenantId!.Value, request.Id, new UpdateInvoiceInput(request.IssueDate, request.DueDate, request.Notes), cancellationToken);
}

public sealed record SubmitInvoiceCommand(Guid Id) : IRequest<InvoiceDto>;

public sealed class SubmitInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<SubmitInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(SubmitInvoiceCommand request, CancellationToken cancellationToken)
        => service.SubmitAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Id, cancellationToken);
}

public sealed record ApproveInvoiceCommand(Guid Id) : IRequest<InvoiceDto>;

public sealed class ApproveInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<ApproveInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(ApproveInvoiceCommand request, CancellationToken cancellationToken)
        => service.ApproveAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Id, cancellationToken);
}

public sealed record RejectInvoiceCommand(Guid Id, string Reason) : IRequest<InvoiceDto>;

public sealed class RejectInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<RejectInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(RejectInvoiceCommand request, CancellationToken cancellationToken)
        => service.RejectAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Id, request.Reason, cancellationToken);
}

public sealed record SendInvoiceCommand(Guid Id) : IRequest<InvoiceDto>;

public sealed class SendInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<SendInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(SendInvoiceCommand request, CancellationToken cancellationToken)
        => service.SendAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Id, cancellationToken);
}

public sealed record VoidInvoiceCommand(Guid Id, string Reason) : IRequest<InvoiceDto>;

public sealed class VoidInvoiceCommandHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<VoidInvoiceCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
        => service.VoidAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Id, request.Reason, cancellationToken);
}
