using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to kb.kb_act_sections (lexflow-database Scripts/09_KB/KbActSections).
/// Module 12 edge case: "Act amendments (section versions with effective dates; reader shows
/// as-on-date selector)". Historical versions of the same (act, number) coexist as soft-deleted
/// rows — the unique index on (act_id, number) only applies WHERE is_deleted = false, so
/// <see cref="Amend"/> closes the current row's effective_to and soft-deletes it in the same
/// operation that inserts a fresh replacement row (see KbActService.AmendSectionAsync).
/// </summary>
public sealed class KbActSection : AuditableEntity
{
    private KbActSection()
    {
    }

    public KbActSection(Guid tenantId, Guid actId, Guid? parentId, string number, string? title, string? body, DateOnly? effectiveFrom, DateOnly? effectiveTo, string? path)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ActId = actId;
        ParentId = parentId;
        Number = number;
        Title = title;
        Body = body;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Path = path;
    }

    public Guid ActId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Number { get; private set; } = null!;
    public string? Title { get; private set; }
    public string? Body { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? Path { get; private set; }

    public void UpdateContent(string? title, string? body) => (Title, Body) = (title, body);

    public void SetPath(string? path) => Path = path;

    /// <summary>Closes this row's validity window at the amendment date and soft-deletes it — the replacement row is a separate KbActSection instance created by the caller.</summary>
    public void CloseForAmendment(DateOnly amendedOn)
    {
        EffectiveTo = amendedOn;
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
