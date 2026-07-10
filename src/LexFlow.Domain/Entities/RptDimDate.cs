namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_dim_date (lexflow-database Scripts/17_Reporting_StarSchema/RptDimDate).
/// Module 13 star schema. Not tenant-scoped (see the table's own 001_Table.sql comment) and not
/// derived from Entity/AuditableEntity — same reasoning as RunningTimer/TaskAssignee.
/// </summary>
public sealed class RptDimDate
{
    private RptDimDate()
    {
    }

    public RptDimDate(int dateKey, DateOnly fullDate)
    {
        DateKey = dateKey;
        FullDate = fullDate;
        Year = fullDate.Year;
        Quarter = (fullDate.Month - 1) / 3 + 1;
        Month = fullDate.Month;
        MonthName = fullDate.ToString("MMMM");
        Day = fullDate.Day;
        DayOfWeek = (int)fullDate.DayOfWeek;
        DayName = fullDate.ToString("dddd");
        IsWeekend = fullDate.DayOfWeek is System.DayOfWeek.Saturday or System.DayOfWeek.Sunday;
        IsoWeek = System.Globalization.ISOWeek.GetWeekOfYear(fullDate.ToDateTime(TimeOnly.MinValue));
    }

    public int DateKey { get; private set; }
    public DateOnly FullDate { get; private set; }
    public int Year { get; private set; }
    public int Quarter { get; private set; }
    public int Month { get; private set; }
    public string MonthName { get; private set; } = null!;
    public int Day { get; private set; }
    public int DayOfWeek { get; private set; }
    public string DayName { get; private set; } = null!;
    public bool IsWeekend { get; private set; }
    public int IsoWeek { get; private set; }

    public static int KeyFor(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;
}
