using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.evidence_custody_log (lexflow-database Scripts/04_Legal/EvidenceCustodyLog). Append-only (DB trigger blocks UPDATE/DELETE) — AC-CC5.</summary>
public sealed class EvidenceCustodyLog : AuditableEntity
{
    private EvidenceCustodyLog()
    {
    }

    public EvidenceCustodyLog(Guid tenantId, Guid evidenceId, string action, string? holder, string? note)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EvidenceId = evidenceId;
        Action = action;
        Holder = holder;
        At = DateTimeOffset.UtcNow;
        Note = note;
    }

    public Guid EvidenceId { get; private set; }
    public string Action { get; private set; } = null!;
    public string? Holder { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Note { get; private set; }
}
