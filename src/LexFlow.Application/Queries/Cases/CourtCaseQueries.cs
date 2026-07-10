using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Cases;

/// <summary>GET /api/v1/cases/{id}.</summary>
public sealed record GetCourtCaseQuery(Guid CaseId) : IRequest<CourtCaseDto>;

public sealed class GetCourtCaseQueryHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<GetCourtCaseQuery, CourtCaseDto>
{
    public async Task<CourtCaseDto> Handle(GetCourtCaseQuery request, CancellationToken cancellationToken)
        => await courtCaseService.GetByIdAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken)
           ?? throw new NotFoundException("CourtCase", request.CaseId);
}

/// <summary>GET /api/v1/matters/{matterId}/cases — practical necessity for the matter workspace's Court Cases tab.</summary>
public sealed record GetCourtCasesByMatterQuery(Guid MatterId) : IRequest<IReadOnlyList<CourtCaseDto>>;

public sealed class GetCourtCasesByMatterQueryHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<GetCourtCasesByMatterQuery, IReadOnlyList<CourtCaseDto>>
{
    public Task<IReadOnlyList<CourtCaseDto>> Handle(GetCourtCasesByMatterQuery request, CancellationToken cancellationToken)
        => courtCaseService.GetByMatterAsync(currentUser.TenantId!.Value, request.MatterId, cancellationToken);
}

public sealed record GetCasePartiesQuery(Guid CaseId) : IRequest<IReadOnlyList<CasePartyDto>>;

public sealed class GetCasePartiesQueryHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<GetCasePartiesQuery, IReadOnlyList<CasePartyDto>>
{
    public Task<IReadOnlyList<CasePartyDto>> Handle(GetCasePartiesQuery request, CancellationToken cancellationToken)
        => courtCaseService.GetPartiesAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken);
}

public sealed record GetCourtOrdersQuery(Guid CaseId) : IRequest<IReadOnlyList<CourtOrderDto>>;

public sealed class GetCourtOrdersQueryHandler(ICourtOrderService courtOrderService, ICurrentUserService currentUser) : IRequestHandler<GetCourtOrdersQuery, IReadOnlyList<CourtOrderDto>>
{
    public Task<IReadOnlyList<CourtOrderDto>> Handle(GetCourtOrdersQuery request, CancellationToken cancellationToken)
        => courtOrderService.GetByCaseAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken);
}

public sealed record GetEvidenceItemsQuery(Guid CaseId) : IRequest<IReadOnlyList<EvidenceItemDto>>;

public sealed class GetEvidenceItemsQueryHandler(IEvidenceService evidenceService, ICurrentUserService currentUser) : IRequestHandler<GetEvidenceItemsQuery, IReadOnlyList<EvidenceItemDto>>
{
    public Task<IReadOnlyList<EvidenceItemDto>> Handle(GetEvidenceItemsQuery request, CancellationToken cancellationToken)
        => evidenceService.GetByCaseAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken);
}

/// <summary>AC-CC5: full custody chain for one evidence item.</summary>
public sealed record GetEvidenceCustodyChainQuery(Guid EvidenceId) : IRequest<IReadOnlyList<EvidenceCustodyLogDto>>;

public sealed class GetEvidenceCustodyChainQueryHandler(IEvidenceService evidenceService, ICurrentUserService currentUser) : IRequestHandler<GetEvidenceCustodyChainQuery, IReadOnlyList<EvidenceCustodyLogDto>>
{
    public Task<IReadOnlyList<EvidenceCustodyLogDto>> Handle(GetEvidenceCustodyChainQuery request, CancellationToken cancellationToken)
        => evidenceService.GetCustodyChainAsync(currentUser.TenantId!.Value, request.EvidenceId, cancellationToken);
}

public sealed record GetWitnessesQuery(Guid CaseId) : IRequest<IReadOnlyList<WitnessDto>>;

public sealed class GetWitnessesQueryHandler(IWitnessService witnessService, ICurrentUserService currentUser) : IRequestHandler<GetWitnessesQuery, IReadOnlyList<WitnessDto>>
{
    public Task<IReadOnlyList<WitnessDto>> Handle(GetWitnessesQuery request, CancellationToken cancellationToken)
        => witnessService.GetByCaseAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken);
}

public sealed record GetArgumentNotesQuery(Guid CaseId) : IRequest<IReadOnlyList<ArgumentNoteDto>>;

public sealed class GetArgumentNotesQueryHandler(IArgumentNoteService argumentNoteService, ICurrentUserService currentUser) : IRequestHandler<GetArgumentNotesQuery, IReadOnlyList<ArgumentNoteDto>>
{
    public Task<IReadOnlyList<ArgumentNoteDto>> Handle(GetArgumentNotesQuery request, CancellationToken cancellationToken)
        => argumentNoteService.GetByCaseAsync(currentUser.TenantId!.Value, request.CaseId, cancellationToken);
}
