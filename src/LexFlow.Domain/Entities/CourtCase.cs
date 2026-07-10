using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.court_cases (lexflow-database Scripts/04_Legal/CourtCases). Module 5.</summary>
public sealed class CourtCase : AuditableEntity
{
    private CourtCase()
    {
    }

    public CourtCase(
        Guid tenantId,
        Guid matterId,
        Guid courtId,
        string caseType,
        string caseNumber,
        int caseYear,
        string? cnrNumber,
        DateOnly? filingDate,
        string? stage,
        Guid? judgeId,
        string? courtroom,
        Guid? appealOfCaseId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        CourtId = courtId;
        CaseType = caseType;
        CaseNumber = caseNumber;
        CaseYear = caseYear;
        CnrNumber = cnrNumber;
        FilingDate = filingDate;
        Stage = stage;
        JudgeId = judgeId;
        Courtroom = courtroom;
        Status = "Active";
        AppealOfCaseId = appealOfCaseId;
    }

    public Guid MatterId { get; private set; }
    public Guid CourtId { get; private set; }
    public string CaseType { get; private set; } = null!;
    public string CaseNumber { get; private set; } = null!;
    public int CaseYear { get; private set; }
    public string? CnrNumber { get; private set; }
    public DateOnly? FilingDate { get; private set; }
    public string? Stage { get; private set; }
    public Guid? JudgeId { get; private set; }
    public string? Courtroom { get; private set; }
    public string Status { get; private set; } = "Active";
    public Guid? AppealOfCaseId { get; private set; }

    public void Update(string? cnrNumber, DateOnly? filingDate, Guid? judgeId, string? courtroom)
    {
        CnrNumber = cnrNumber;
        FilingDate = filingDate;
        JudgeId = judgeId;
        Courtroom = courtroom;
    }

    public void ChangeStage(string stage) => Stage = stage;

    public void SetStatus(string status) => Status = status;
}
