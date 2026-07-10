using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Matters;

/// <summary>PUT /api/v1/matters/{id}.</summary>
public sealed record UpdateMatterCommand(
    Guid MatterId,
    string Title,
    string MatterType,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? ResponsibleLawyerId,
    string Priority,
    string? Description,
    decimal? Budget,
    bool IsPrivate,
    string? BillingArrangementJson) : IRequest<MatterDto>;

public sealed class UpdateMatterCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<UpdateMatterCommand, MatterDto>
{
    public Task<MatterDto> Handle(UpdateMatterCommand request, CancellationToken cancellationToken)
        => matterService.UpdateAsync(
            currentUser.TenantId!.Value,
            request.MatterId,
            new UpdateMatterInput(request.Title, request.MatterType, request.PracticeAreaId, request.BranchId, request.ResponsibleLawyerId, request.Priority, request.Description, request.Budget, request.IsPrivate, request.BillingArrangementJson),
            cancellationToken);
}
