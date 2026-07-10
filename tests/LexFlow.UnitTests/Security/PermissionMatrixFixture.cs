namespace LexFlow.UnitTests.Security;

/// <summary>
/// Transcribes PRD §21's Roles &amp; Permissions Matrix as seeded in
/// lexflow-database Scripts/16_Seed/001_Permissions_Catalog.sql (the full catalog)
/// and 002_System_Roles.sql (role -&gt; permission wiring), so the G-AC4 permission
/// test suite exercises the same ground truth the database seed does. Client(Portal)
/// is excluded — see those seed files' header comments for why it has no core.roles row.
/// </summary>
public static class PermissionMatrixFixture
{
    public static readonly IReadOnlyList<string> AllPermissionKeys =
    [
        "leads.read.own", "leads.read.team", "leads.read.all",
        "leads.create.own", "leads.create.team", "leads.create.branch", "leads.create.all",
        "leads.update.own", "leads.update.team", "leads.update.all",
        "leads.convert.own", "leads.convert.team", "leads.convert.all",

        "clients.read.own", "clients.read.team", "clients.read.all",
        "clients.create.own", "clients.create.team", "clients.create.all",
        "clients.update.own", "clients.update.team", "clients.update.all",

        "clients_kyc.read.own", "clients_kyc.read.team", "clients_kyc.read.all",

        "matters.read.own", "matters.read.team", "matters.read.all",
        "matters.create.own", "matters.create.team", "matters.create.all",
        "matters.update.own", "matters.update.team", "matters.update.all",
        "matters.close.team", "matters.close.all",
        "matters.conflict_override.all",

        "hearings.read.own", "hearings.read.team", "hearings.read.all",
        "hearings.create.own", "hearings.create.team", "hearings.create.all",
        "hearings.update.own", "hearings.update.team", "hearings.update.all",

        "documents.read.own", "documents.read.team", "documents.read.all",
        "documents.create.own", "documents.create.team", "documents.create.all",
        "documents.update.own", "documents.update.team", "documents.update.all",
        "documents.share.own", "documents.share.team", "documents.share.all",

        "documents_privileged.read.own", "documents_privileged.read.team", "documents_privileged.read.all",

        "time_entries.read.own", "time_entries.read.team", "time_entries.read.all",
        "time_entries.create.own", "time_entries.update.own", "time_entries.delete.own",
        "time_entries.approve.team",

        "rates.manage.all", "rates.read.team", "rates.read.all",

        "invoices.read.own", "invoices.read.all",
        "invoices.create.own", "invoices.create.team", "invoices.create.all",
        "invoices.update.own", "invoices.update.team", "invoices.update.all",
        "invoices.approve.team", "invoices.approve.all",
        "invoices.send.all", "invoices.void.all",

        "payments.record.all", "payments.refund.all", "payments.read.all", "payments.pay.own",

        "trust.deposit.all", "trust.disburse.all", "trust.read.all",

        "tasks.manage.own", "tasks.manage.team", "tasks.manage.all", "tasks.read.all",

        "calendar.read.own", "calendar.read.team", "calendar.read.branch", "calendar.read.all",

        "comm.read.own", "comm.read.team", "comm.read.all",
        "comm.send.own", "comm.send.team", "comm.send.all",

        "kb.read.all", "kb.contribute.all", "kb.review.all",

        "reports_operational.read.own", "reports_operational.read.team", "reports_operational.read.all",
        "reports_financial.read.team", "reports_financial.read.all",

        "users.manage.all", "users.read.all",

        "settings.manage.all", "settings.read.all",

        "audit.read.own", "audit.read.all",

        "export.data.own", "export.data.team", "export.data.all",
    ];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RoleGrants = new Dictionary<string, IReadOnlyList<string>>
    {
        ["owner"] =
        [
            "leads.read.all", "leads.create.all", "leads.update.all", "leads.convert.all",
            "clients.read.all", "clients.create.all", "clients.update.all",
            "clients_kyc.read.all",
            "matters.read.all", "matters.create.all", "matters.update.all", "matters.close.all", "matters.conflict_override.all",
            "hearings.read.all", "hearings.create.all", "hearings.update.all",
            "documents.read.all", "documents.create.all", "documents.update.all", "documents.share.all",
            "documents_privileged.read.all",
            "time_entries.read.all",
            "rates.manage.all",
            "invoices.create.all", "invoices.update.all", "invoices.approve.all", "invoices.send.all", "invoices.void.all",
            "payments.record.all", "payments.refund.all",
            "trust.deposit.all", "trust.disburse.all",
            "tasks.manage.all",
            "calendar.read.all",
            "comm.read.all", "comm.send.all",
            "kb.read.all", "kb.contribute.all", "kb.review.all",
            "reports_operational.read.all", "reports_financial.read.all",
            "users.manage.all",
            "settings.manage.all",
            "audit.read.all",
            "export.data.all",
        ],
        ["admin"] =
        [
            "leads.read.all", "leads.create.all", "leads.update.all", "leads.convert.all",
            "clients.read.all", "clients.create.all", "clients.update.all",
            "tasks.manage.all",
            "calendar.read.all",
            "kb.read.all", "kb.contribute.all", "kb.review.all",
            "reports_operational.read.all",
            "users.manage.all",
            "settings.manage.all",
            "audit.read.all",
            "export.data.all",
        ],
        ["senior_partner"] =
        [
            "leads.read.all", "leads.create.all", "leads.update.all", "leads.convert.all",
            "clients.read.all", "clients.create.all", "clients.update.all",
            "clients_kyc.read.all",
            "matters.read.all", "matters.create.all", "matters.update.all", "matters.close.all", "matters.conflict_override.all",
            "hearings.read.all", "hearings.create.all", "hearings.update.all",
            "documents.read.all", "documents.create.all", "documents.update.all", "documents.share.all",
            "documents_privileged.read.all",
            "time_entries.read.team", "time_entries.approve.team",
            "rates.manage.all",
            "invoices.create.all", "invoices.update.all", "invoices.approve.all", "invoices.send.all", "invoices.void.all",
            "tasks.manage.all",
            "calendar.read.all",
            "comm.read.all", "comm.send.all",
            "kb.read.all", "kb.contribute.all", "kb.review.all",
            "reports_operational.read.all", "reports_financial.read.all",
            "export.data.all",
        ],
        ["partner"] =
        [
            "leads.read.team", "leads.create.team", "leads.update.team", "leads.convert.team",
            "clients.read.team", "clients.create.team", "clients.update.team",
            "clients_kyc.read.team",
            "matters.read.team", "matters.create.team", "matters.update.team", "matters.close.team",
            "hearings.read.team", "hearings.create.team", "hearings.update.team",
            "documents.read.team", "documents.create.team", "documents.update.team", "documents.share.team",
            "documents_privileged.read.team",
            "time_entries.read.team", "time_entries.approve.team",
            "rates.read.team",
            "invoices.create.team", "invoices.update.team", "invoices.approve.team",
            "tasks.manage.team",
            "calendar.read.team",
            "comm.read.team", "comm.send.team",
            "kb.read.all", "kb.contribute.all", "kb.review.all",
            "reports_operational.read.team", "reports_financial.read.team",
            "export.data.team",
        ],
        ["lawyer"] =
        [
            "leads.read.own", "leads.create.own", "leads.update.own", "leads.convert.own",
            "clients.read.own", "clients.create.own", "clients.update.own",
            "clients_kyc.read.own",
            "matters.read.own", "matters.create.own", "matters.update.own",
            "hearings.read.own", "hearings.create.own", "hearings.update.own",
            "documents.read.own", "documents.create.own", "documents.update.own", "documents.share.own",
            "documents_privileged.read.own",
            "time_entries.read.own", "time_entries.create.own", "time_entries.update.own", "time_entries.delete.own",
            "invoices.create.own", "invoices.update.own",
            "tasks.manage.own",
            "calendar.read.own",
            "comm.read.own", "comm.send.own",
            "kb.read.all", "kb.contribute.all",
            "reports_operational.read.own",
        ],
        ["paralegal"] =
        [
            "leads.read.team", "leads.create.team", "leads.update.team",
            "clients.read.team", "clients.update.team",
            "clients_kyc.read.team",
            "matters.read.team",
            "hearings.read.team", "hearings.create.team", "hearings.update.team",
            "documents.read.team", "documents.create.team", "documents.update.team", "documents.share.team",
            "time_entries.read.own", "time_entries.create.own", "time_entries.update.own", "time_entries.delete.own",
            "tasks.manage.own",
            "calendar.read.team",
            "comm.read.team", "comm.send.team",
            "kb.read.all", "kb.contribute.all",
        ],
        ["receptionist"] =
        [
            "leads.read.own", "leads.create.branch",
            "clients.read.own",
            "tasks.manage.own",
            "calendar.read.branch",
            "comm.send.own",
        ],
        ["finance"] =
        [
            "clients.read.all",
            "clients_kyc.read.all",
            "matters.read.all",
            "documents.read.own",
            "time_entries.read.all",
            "rates.manage.all",
            "invoices.create.all", "invoices.update.all", "invoices.send.all", "invoices.void.all",
            "payments.record.all", "payments.refund.all",
            "trust.deposit.all", "trust.disburse.all",
            "tasks.manage.own",
            "calendar.read.own",
            "kb.read.all",
            "reports_operational.read.all", "reports_financial.read.all",
            "settings.manage.all",
            "audit.read.own",
            "export.data.all",
        ],
        ["hr"] =
        [
            "tasks.manage.own",
            "calendar.read.own",
            "reports_operational.read.team",
            "users.manage.all",
            "audit.read.own",
        ],
        ["auditor"] =
        [
            "leads.read.all",
            "clients.read.all", "clients_kyc.read.all",
            "matters.read.all",
            "hearings.read.all",
            "documents.read.all", "documents_privileged.read.all",
            "time_entries.read.all",
            "rates.read.all",
            "invoices.read.all",
            "payments.read.all",
            "trust.read.all",
            "tasks.read.all",
            "calendar.read.all",
            "comm.read.all",
            "kb.read.all",
            "reports_operational.read.all", "reports_financial.read.all",
            "users.read.all",
            "settings.read.all",
            "audit.read.all",
            "export.data.all",
        ],
    };
}
