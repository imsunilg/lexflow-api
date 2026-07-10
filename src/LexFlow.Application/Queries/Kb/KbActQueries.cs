using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Kb;

public sealed record GetKbActsQuery : IRequest<IReadOnlyList<KbActDto>>;

public sealed class GetKbActsQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<GetKbActsQuery, IReadOnlyList<KbActDto>>
{
    public Task<IReadOnlyList<KbActDto>> Handle(GetKbActsQuery request, CancellationToken cancellationToken)
        => service.GetActsAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetKbActQuery(Guid Id) : IRequest<KbActDto?>;

public sealed class GetKbActQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<GetKbActQuery, KbActDto?>
{
    public Task<KbActDto?> Handle(GetKbActQuery request, CancellationToken cancellationToken)
        => service.GetActAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetKbActSectionsQuery(Guid ActId) : IRequest<IReadOnlyList<KbActSectionDto>>;

public sealed class GetKbActSectionsQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<GetKbActSectionsQuery, IReadOnlyList<KbActSectionDto>>
{
    public Task<IReadOnlyList<KbActSectionDto>> Handle(GetKbActSectionsQuery request, CancellationToken cancellationToken)
        => service.GetSectionsAsync(currentUser.TenantId!.Value, request.ActId, cancellationToken);
}

public sealed record GetKbActSectionQuery(Guid Id) : IRequest<KbActSectionDto?>;

public sealed class GetKbActSectionQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<GetKbActSectionQuery, KbActSectionDto?>
{
    public Task<KbActSectionDto?> Handle(GetKbActSectionQuery request, CancellationToken cancellationToken)
        => service.GetSectionAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

/// <summary>AC-KB1: "IPC 420" -&gt; direct section jump.</summary>
public sealed record LookupKbActSectionQuery(string Act, string Number) : IRequest<KbActSectionDto?>;

public sealed class LookupKbActSectionQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<LookupKbActSectionQuery, KbActSectionDto?>
{
    public Task<KbActSectionDto?> Handle(LookupKbActSectionQuery request, CancellationToken cancellationToken)
        => service.LookupSectionAsync(currentUser.TenantId!.Value, request.Act, request.Number, cancellationToken);
}

/// <summary>AC-KB3: as-on-date view of an amended section.</summary>
public sealed record GetKbActSectionAsOfQuery(Guid ActId, string Number, DateOnly AsOf) : IRequest<KbActSectionDto?>;

public sealed class GetKbActSectionAsOfQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<GetKbActSectionAsOfQuery, KbActSectionDto?>
{
    public Task<KbActSectionDto?> Handle(GetKbActSectionAsOfQuery request, CancellationToken cancellationToken)
        => service.GetSectionAsOfAsync(currentUser.TenantId!.Value, request.ActId, request.Number, request.AsOf, cancellationToken);
}

public sealed record GetKbActSectionHistoryQuery(Guid ActId, string Number) : IRequest<IReadOnlyList<KbActSectionDto>>;

public sealed class GetKbActSectionHistoryQueryHandler(IKbActService service, ICurrentUserService currentUser) : IRequestHandler<GetKbActSectionHistoryQuery, IReadOnlyList<KbActSectionDto>>
{
    public Task<IReadOnlyList<KbActSectionDto>> Handle(GetKbActSectionHistoryQuery request, CancellationToken cancellationToken)
        => service.GetSectionHistoryAsync(currentUser.TenantId!.Value, request.ActId, request.Number, cancellationToken);
}
