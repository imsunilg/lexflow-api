using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to kb.kb_articles (lexflow-database Scripts/09_KB/KbArticles).
/// Module 12: "articles support draft → peer review → publish (versioned)". BR (peer-review
/// rule): "article publish requires ≥1 tag + reviewer ≠ author" — the reviewer≠author half is
/// enforced first at the DB (kb.kb_articles 004_Triggers.sql: BEFORE INSERT OR UPDATE blocks any
/// row reaching status=Published with reviewer_id = author_id) and again defensively here in
/// <see cref="Publish"/>, so the application layer never even attempts the doomed UPDATE — defense
/// in depth, matching every other DB-trigger-backed invariant in this codebase.
/// </summary>
public sealed class KbArticle : AuditableEntity
{
    private KbArticle()
    {
    }

    public KbArticle(Guid tenantId, string title, string? body, Guid? authorId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Title = title;
        Body = body;
        Status = "Draft";
        Version = 1;
        AuthorId = authorId;
    }

    public string Title { get; private set; } = null!;
    public string? Body { get; private set; }
    public string Status { get; private set; } = "Draft";
    public int Version { get; private set; } = 1;
    public Guid? AuthorId { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public void UpdateDraft(string title, string? body)
    {
        if (Status == "Published")
        {
            throw new InvalidOperationException($"Article {Id} is Published — edit via a new draft revision, not in place.");
        }

        Title = title;
        Body = body;
        Status = "Draft";
        ReviewerId = null;
    }

    public void SubmitForReview()
    {
        if (Status != "Draft")
        {
            throw new InvalidOperationException($"Article {Id} must be Draft to submit for review (current status {Status}).");
        }

        Status = "InReview";
    }

    /// <summary>Assigns the reviewer at the point review starts, distinct from the author (checked here; the DB trigger checks again at Publish, the only status the constraint actually gates).</summary>
    public void AssignReviewer(Guid reviewerId)
    {
        if (Status != "InReview")
        {
            throw new InvalidOperationException($"Article {Id} must be InReview to assign a reviewer (current status {Status}).");
        }

        if (reviewerId == AuthorId)
        {
            throw new InvalidOperationException("The reviewer must differ from the author (Module 12 peer-review rule).");
        }

        ReviewerId = reviewerId;
    }

    public void SendBackToDraft()
    {
        Status = "Draft";
        ReviewerId = null;
    }

    public void Publish(bool hasAtLeastOneTag)
    {
        if (Status != "InReview")
        {
            throw new InvalidOperationException($"Article {Id} must be InReview to publish (current status {Status}).");
        }

        if (ReviewerId is null || ReviewerId == AuthorId)
        {
            throw new InvalidOperationException("Cannot publish — reviewer must differ from author (Module 12 peer-review rule).");
        }

        if (!hasAtLeastOneTag)
        {
            throw new InvalidOperationException("Cannot publish — at least one tag is required (Module 12 Validation Rules).");
        }

        Status = "Published";
        PublishedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}
