using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to fin.number_series (lexflow-database Scripts/06_Fin/NumberSeries). Backs Settings §10 Number Series.</summary>
public sealed class NumberSeries : AuditableEntity
{
    private NumberSeries()
    {
    }

    public NumberSeries(Guid tenantId, string seriesKey, int fiscalYear, string formatPattern, Guid? branchId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        BranchId = branchId;
        SeriesKey = seriesKey;
        FiscalYear = fiscalYear;
        FormatPattern = formatPattern;
        NextSeq = 1;
    }

    public Guid? BranchId { get; private set; }
    public string SeriesKey { get; private set; } = null!;
    public int FiscalYear { get; private set; }
    public string FormatPattern { get; private set; } = null!;
    public long NextSeq { get; private set; }

    /// <summary>Pattern changes only apply going forward (PRD Module 15 Edge Cases) — next_seq/history are untouched.</summary>
    public void UpdatePattern(string formatPattern) => FormatPattern = formatPattern;

    public long PeekNext() => NextSeq;

    public long ConsumeNext()
    {
        var value = NextSeq;
        NextSeq++;
        return value;
    }
}
