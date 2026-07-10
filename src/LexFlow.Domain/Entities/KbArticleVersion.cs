using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to kb.kb_article_versions (lexflow-database Scripts/09_KB/KbArticleVersions). One snapshot row per publish, per Module 12: "articles support draft → peer review → publish (versioned)".</summary>
public sealed class KbArticleVersion : AuditableEntity
{
    private KbArticleVersion()
    {
    }

    public KbArticleVersion(Guid tenantId, Guid articleId, int versionNo, string? title, string? body, Guid? authorId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ArticleId = articleId;
        VersionNo = versionNo;
        Title = title;
        Body = body;
        AuthorId = authorId;
    }

    public Guid ArticleId { get; private set; }
    public int VersionNo { get; private set; }
    public string? Title { get; private set; }
    public string? Body { get; private set; }
    public Guid? AuthorId { get; private set; }
}
