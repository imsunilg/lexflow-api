using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Templates;

public sealed record GetCommTemplatesQuery(string? Channel) : IRequest<IReadOnlyList<CommTemplateDto>>;

public sealed class GetCommTemplatesQueryHandler(ICommTemplateService commTemplateService, ICurrentUserService currentUser)
    : IRequestHandler<GetCommTemplatesQuery, IReadOnlyList<CommTemplateDto>>
{
    public Task<IReadOnlyList<CommTemplateDto>> Handle(GetCommTemplatesQuery request, CancellationToken cancellationToken)
        => commTemplateService.GetAllAsync(currentUser.TenantId!.Value, request.Channel, cancellationToken);
}
