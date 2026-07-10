using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Matters;

/// <summary>POST /api/v1/matters (PRD §17, Module 4). AC-M1: conflict check fires when opposite parties are supplied.</summary>
public sealed record CreateMatterCommand(
    string Title,
    Guid ClientId,
    string MatterType,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? ResponsibleLawyerId,
    string Priority,
    string? Description,
    DateOnly OpenedOn,
    bool IsPrivate,
    decimal? Budget,
    string? BillingArrangementJson,
    IReadOnlyList<string>? OppositePartyNames,
    bool OverrideConflict,
    string? ConflictOverrideReason) : IRequest<MatterDto>;

public sealed class CreateMatterCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<CreateMatterCommand, MatterDto>
{
    public Task<MatterDto> Handle(CreateMatterCommand request, CancellationToken cancellationToken)
        => matterService.CreateAsync(
            currentUser.TenantId!.Value,
            currentUser.UserId,
            new CreateMatterInput(
                request.Title, request.ClientId, request.MatterType, request.PracticeAreaId, request.BranchId, request.ResponsibleLawyerId,
                request.Priority, request.Description, request.OpenedOn, request.IsPrivate, request.Budget, request.BillingArrangementJson,
                request.OppositePartyNames, request.OverrideConflict, request.ConflictOverrideReason),
            cancellationToken);
}
