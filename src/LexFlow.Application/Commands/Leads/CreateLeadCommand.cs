using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Leads;

/// <summary>POST /api/v1/leads (PRD §17, Module 2).</summary>
public sealed record CreateLeadCommand(
    string FirstName,
    string? LastName,
    string? Company,
    string? Email,
    string? PhoneE164,
    Guid? SourceId,
    Guid? OwnerId,
    Guid? BranchId,
    Guid? PracticeAreaId,
    string? IssueSummary,
    string? OpposingParty,
    string? BudgetBand) : IRequest<LeadDto>;

public sealed class CreateLeadCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<CreateLeadCommand, LeadDto>
{
    public Task<LeadDto> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
        => leadService.CreateAsync(
            currentUser.TenantId!.Value,
            currentUser.UserId,
            new CreateLeadInput(
                request.FirstName, request.LastName, request.Company, request.Email, request.PhoneE164,
                request.SourceId, request.OwnerId, request.BranchId, request.PracticeAreaId,
                request.IssueSummary, request.OpposingParty, request.BudgetBand),
            cancellationToken);
}
