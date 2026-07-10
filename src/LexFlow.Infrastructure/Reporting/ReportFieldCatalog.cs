namespace LexFlow.Infrastructure.Reporting;

/// <summary>
/// Module 13 Custom Report Builder whitelist: "choose columns (whitelisted field catalog with
/// joins pre-modeled)"; Validation: "custom builder field whitelist only (no raw SQL ever)".
/// Pure, static, and side-effect-free (mirrors <c>GstCalculator</c>/<c>KbCitationQueryParser</c>
/// in this same layer) so both <c>CustomReportService</c> and its tests can check a field key
/// without spinning up a DbContext. Every column/filter/group-by/aggregate key a caller submits
/// is checked against this catalog before <c>CustomReportService</c> builds anything — an unknown
/// key is rejected outright, never passed through to a query.
/// </summary>
public static class ReportFieldCatalog
{
    public static readonly IReadOnlyList<string> BaseEntities = ["Matter", "Invoice", "TimeEntry", "Lead", "Task", "Hearing"];

    private static readonly Dictionary<string, IReadOnlyDictionary<string, ReportFieldDefinition>> FieldsByEntity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Matter"] = Fields(
            F("Id", "Number", ReportFieldType.Text),
            F("Number", "Number", ReportFieldType.Text),
            F("Title", "Title", ReportFieldType.Text),
            F("Status", "Status", ReportFieldType.Text),
            F("Priority", "Priority", ReportFieldType.Text),
            F("OpenedOn", "Opened On", ReportFieldType.Date),
            F("ClosedOn", "Closed On", ReportFieldType.Date),
            F("PracticeAreaId", "Practice Area", ReportFieldType.Text),
            F("ResponsibleLawyerId", "Responsible Lawyer", ReportFieldType.Text),
            F("ClientId", "Client", ReportFieldType.Text),
            F("BranchId", "Branch", ReportFieldType.Text),
            F("Budget", "Budget", ReportFieldType.Number)),
        ["Invoice"] = Fields(
            F("Id", "Id", ReportFieldType.Text),
            F("Number", "Number", ReportFieldType.Text),
            F("Status", "Status", ReportFieldType.Text),
            F("IssueDate", "Issue Date", ReportFieldType.Date),
            F("DueDate", "Due Date", ReportFieldType.Date),
            F("GrandTotal", "Grand Total", ReportFieldType.Number),
            F("AmountPaid", "Amount Paid", ReportFieldType.Number),
            F("TaxTotal", "Tax Total", ReportFieldType.Number),
            F("ClientId", "Client", ReportFieldType.Text),
            F("MatterId", "Matter", ReportFieldType.Text)),
        ["TimeEntry"] = Fields(
            F("Id", "Id", ReportFieldType.Text),
            F("UserId", "Timekeeper", ReportFieldType.Text),
            F("MatterId", "Matter", ReportFieldType.Text),
            F("EntryDate", "Entry Date", ReportFieldType.Date),
            F("DurationMin", "Duration (min)", ReportFieldType.Number),
            F("RoundedMin", "Rounded (min)", ReportFieldType.Number),
            F("Billable", "Billable", ReportFieldType.Boolean),
            F("Status", "Status", ReportFieldType.Text),
            F("AmountSnapshot", "Amount", ReportFieldType.Number)),
        ["Lead"] = Fields(
            F("Id", "Id", ReportFieldType.Text),
            F("Number", "Number", ReportFieldType.Text),
            F("Stage", "Stage", ReportFieldType.Text),
            F("Status", "Status", ReportFieldType.Text),
            F("OwnerId", "Owner", ReportFieldType.Text),
            F("BranchId", "Branch", ReportFieldType.Text),
            F("PracticeAreaId", "Practice Area", ReportFieldType.Text),
            F("SourceId", "Source", ReportFieldType.Text),
            F("Score", "Score", ReportFieldType.Number)),
        ["Task"] = Fields(
            F("Id", "Id", ReportFieldType.Text),
            F("Title", "Title", ReportFieldType.Text),
            F("Status", "Status", ReportFieldType.Text),
            F("Priority", "Priority", ReportFieldType.Text),
            F("OwnerId", "Owner", ReportFieldType.Text),
            F("MatterId", "Matter", ReportFieldType.Text),
            F("DueAt", "Due At", ReportFieldType.Date),
            F("ProgressPct", "Progress %", ReportFieldType.Number)),
        ["Hearing"] = Fields(
            F("Id", "Id", ReportFieldType.Text),
            F("CaseId", "Case", ReportFieldType.Text),
            F("Date", "Date", ReportFieldType.Date),
            F("Status", "Status", ReportFieldType.Text),
            F("AssignedLawyerId", "Assigned Lawyer", ReportFieldType.Text),
            F("Courtroom", "Courtroom", ReportFieldType.Text)),
    };

    public static bool IsValidBaseEntity(string baseEntity) => FieldsByEntity.ContainsKey(baseEntity);

    public static IReadOnlyList<ReportFieldDefinition> GetFields(string baseEntity) =>
        FieldsByEntity.TryGetValue(baseEntity, out var fields) ? fields.Values.ToList() : [];

    public static bool IsValidField(string baseEntity, string field) =>
        FieldsByEntity.TryGetValue(baseEntity, out var fields) && fields.ContainsKey(field);

    private static ReportFieldDefinition F(string key, string label, ReportFieldType type) => new(key, label, type);

    private static IReadOnlyDictionary<string, ReportFieldDefinition> Fields(params ReportFieldDefinition[] fields) =>
        fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
}

public enum ReportFieldType
{
    Text,
    Number,
    Date,
    Boolean,
}

public sealed record ReportFieldDefinition(string Key, string Label, ReportFieldType Type);
