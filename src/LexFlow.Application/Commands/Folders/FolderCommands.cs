using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Folders;

/// <summary>POST /api/v1/folders (PRD §17, Module 7).</summary>
public sealed record CreateFolderCommand(string Name, Guid? ParentId, Guid? MatterId) : IRequest<FolderDto>;

public sealed class CreateFolderCommandHandler(IFolderService folderService, ICurrentUserService currentUser) : IRequestHandler<CreateFolderCommand, FolderDto>
{
    public Task<FolderDto> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
        => folderService.CreateAsync(currentUser.TenantId!.Value, currentUser.UserId, request.Name, request.ParentId, request.MatterId, cancellationToken);
}

/// <summary>PUT /api/v1/folders/{id}.</summary>
public sealed record UpdateFolderCommand(Guid FolderId, string Name) : IRequest<FolderDto>;

public sealed class UpdateFolderCommandHandler(IFolderService folderService, ICurrentUserService currentUser) : IRequestHandler<UpdateFolderCommand, FolderDto>
{
    public Task<FolderDto> Handle(UpdateFolderCommand request, CancellationToken cancellationToken)
        => folderService.UpdateAsync(currentUser.TenantId!.Value, request.FolderId, request.Name, cancellationToken);
}

/// <summary>DELETE /api/v1/folders/{id}.</summary>
public sealed record DeleteFolderCommand(Guid FolderId) : IRequest;

public sealed class DeleteFolderCommandHandler(IFolderService folderService, ICurrentUserService currentUser) : IRequestHandler<DeleteFolderCommand>
{
    public async Task Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
        => await folderService.DeleteAsync(currentUser.TenantId!.Value, request.FolderId, cancellationToken);
}

/// <summary>POST /api/v1/folders/{id}/move.</summary>
public sealed record MoveFolderCommand(Guid FolderId, Guid? NewParentId) : IRequest<FolderDto>;

public sealed class MoveFolderCommandHandler(IFolderService folderService, ICurrentUserService currentUser) : IRequestHandler<MoveFolderCommand, FolderDto>
{
    public Task<FolderDto> Handle(MoveFolderCommand request, CancellationToken cancellationToken)
        => folderService.MoveAsync(currentUser.TenantId!.Value, request.FolderId, request.NewParentId, cancellationToken);
}
