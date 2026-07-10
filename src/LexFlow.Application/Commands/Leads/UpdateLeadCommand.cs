using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Leads;

/// <summary>PUT /api/v1/leads/{id}.</summary>
public sealed record UpdateLeadCommand(
    Guid LeadId,
    string FirstName,
    string? LastName,
    string? Company,
    string? Email,
    string? PhoneE164,
    Guid? SourceId,
    Guid? PracticeAreaId,
    string? IssueSummary,
    string? OpposingParty,
    string? BudgetBand) : IRequest<LeadDto>;

public sealed class UpdateLeadCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<UpdateLeadCommand, LeadDto>
{
    public Task<LeadDto> Handle(UpdateLeadCommand request, CancellationToken cancellationToken)
        => leadService.UpdateAsync(
            currentUser.TenantId!.Value,
            request.LeadId,
            new UpdateLeadInput(
                request.FirstName, request.LastName, request.Company, request.Email, request.PhoneE164,
                request.SourceId, request.PracticeAreaId, request.IssueSummary, request.OpposingParty, request.BudgetBand),
            cancellationToken);
}

/// <summary>DELETE /api/v1/leads/{id}.</summary>
public sealed record DeleteLeadCommand(Guid LeadId) : IRequest;

public sealed class DeleteLeadCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<DeleteLeadCommand>
{
    public async Task Handle(DeleteLeadCommand request, CancellationToken cancellationToken)
        => await leadService.DeleteAsync(currentUser.TenantId!.Value, request.LeadId, cancellationToken);
}
