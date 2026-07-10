using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

public sealed class PortalScopeService(LexFlowDbContext db) : IPortalScopeService
{
    public async Task<Guid[]?> GetVisibleMatterIdsAsync(Guid tenantId, Guid portalUserId, CancellationToken cancellationToken = default)
    {
        var portalUser = await db.ClientPortalUsers.SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == portalUserId, cancellationToken);
        return portalUser?.VisibleMatterIds;
    }
}
