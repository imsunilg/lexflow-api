using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.court_holidays (lexflow-database Scripts/04_Legal/CourtHolidays).</summary>
public sealed class CourtHoliday : AuditableEntity
{
    private CourtHoliday()
    {
    }

    public CourtHoliday(Guid tenantId, Guid courtId, DateOnly holidayDate, string? name)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CourtId = courtId;
        HolidayDate = holidayDate;
        Name = name;
    }

    public Guid CourtId { get; private set; }
    public DateOnly HolidayDate { get; private set; }
    public string? Name { get; private set; }
}
