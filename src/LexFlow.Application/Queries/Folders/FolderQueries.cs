using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Folders;

public sealed record GetFolderQuery(Guid FolderId) : IRequest<FolderDto>;

public sealed class GetFolderQueryHandler(IFolderService folderService, ICurrentUserService currentUser) : IRequestHandler<GetFolderQuery, FolderDto>
{
    public async Task<FolderDto> Handle(GetFolderQuery request, CancellationToken cancellationToken)
        => await folderService.GetByIdAsync(currentUser.TenantId!.Value, request.FolderId, cancellationToken)
           ?? throw new NotFoundException("Folder", request.FolderId);
}

public sealed record GetFoldersQuery(Guid? ParentId, Guid? MatterId) : IRequest<IReadOnlyList<FolderDto>>;

public sealed class GetFoldersQueryHandler(IFolderService folderService, ICurrentUserService currentUser) : IRequestHandler<GetFoldersQuery, IReadOnlyList<FolderDto>>
{
    public Task<IReadOnlyList<FolderDto>> Handle(GetFoldersQuery request, CancellationToken cancellationToken)
        => folderService.GetAllAsync(currentUser.TenantId!.Value, request.ParentId, request.MatterId, cancellationToken);
}
