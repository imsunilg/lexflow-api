using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

public sealed class CourtOrderService(LexFlowDbContext db) : ICourtOrderService
{
    public async Task<CourtOrderDto> AddAsync(Guid tenantId, Guid caseId, Guid? hearingId, DateOnly orderDate, string? gist, DateOnly? complianceDue, Guid? documentId, CancellationToken cancellationToken = default)
    {
        var exists = await db.CourtCases.AnyAsync(c => c.TenantId == tenantId && c.Id == caseId, cancellationToken);
        if (!exists)
        {
            throw new Application.Common.Exceptions.NotFoundException(nameof(CourtCase), caseId);
        }

        var order = new CourtOrder(tenantId, caseId, hearingId, orderDate, gist, complianceDue, documentId);
        await db.CourtOrders.AddAsync(order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(order);
    }

    public async Task<IReadOnlyList<CourtOrderDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var orders = await db.CourtOrders.Where(o => o.TenantId == tenantId && o.CaseId == caseId).OrderByDescending(o => o.OrderDate).ToListAsync(cancellationToken);
        return orders.Select(ToDto).ToList();
    }

    private static CourtOrderDto ToDto(CourtOrder o) => new(o.Id, o.CaseId, o.HearingId, o.OrderDate, o.Gist, o.ComplianceDue, o.DocumentId);
}
