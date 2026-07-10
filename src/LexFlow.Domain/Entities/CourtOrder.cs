using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.court_orders (lexflow-database Scripts/04_Legal/CourtOrders).</summary>
public sealed class CourtOrder : AuditableEntity
{
    private CourtOrder()
    {
    }

    public CourtOrder(Guid tenantId, Guid caseId, Guid? hearingId, DateOnly orderDate, string? gist, DateOnly? complianceDue, Guid? documentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CaseId = caseId;
        HearingId = hearingId;
        OrderDate = orderDate;
        Gist = gist;
        ComplianceDue = complianceDue;
        DocumentId = documentId;
    }

    public Guid CaseId { get; private set; }
    public Guid? HearingId { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public string? Gist { get; private set; }
    public DateOnly? ComplianceDue { get; private set; }
    public Guid? DocumentId { get; private set; }
}
