using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace LexFlow.Api.Swagger;

/// <summary>
/// Maps each controller to the "Area" it belongs to per PRD §16's API List table, so the
/// generated OpenAPI document's tags mirror that table instead of one tag per controller class
/// (e.g. the six Comm* controllers and six Kb* controllers each collapse to one tag).
/// </summary>
public static class SwaggerAreaTags
{
    private static readonly Dictionary<string, string> AreaByController = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Auth"] = "Auth",
        ["Leads"] = "Leads",
        ["PublicLeadCapture"] = "Leads",
        ["Clients"] = "Clients",
        ["Matters"] = "Matters",
        ["Cases"] = "Cases",
        ["Hearings"] = "Cases",
        ["Evidence"] = "Cases",
        ["LegalLookups"] = "Cases",
        ["Calendar"] = "Calendar",
        ["PublicCalendar"] = "Calendar",
        ["Documents"] = "Documents",
        ["Folders"] = "Documents",
        ["DocumentTemplates"] = "Documents",
        ["ShareLinks"] = "Documents",
        ["PublicDocumentShare"] = "Documents",
        ["SignatureWebhook"] = "Documents",
        ["Invoices"] = "Billing",
        ["BillingReports"] = "Billing",
        ["Payments"] = "Billing",
        ["Trust"] = "Billing",
        ["RateCards"] = "Billing",
        ["BillingArrangements"] = "Billing",
        ["Dunning"] = "Billing",
        ["Timers"] = "Time",
        ["TimeEntries"] = "Time",
        ["Tasks"] = "Tasks",
        ["TaskTemplates"] = "Tasks",
        ["CommEmail"] = "Comm",
        ["CommSms"] = "Comm",
        ["CommWhatsApp"] = "Comm",
        ["CommCalls"] = "Comm",
        ["CommTimeline"] = "Comm",
        ["Chat"] = "Comm",
        ["KbActs"] = "KB",
        ["KbJudgments"] = "KB",
        ["KbArticles"] = "KB",
        ["KbTaxonomy"] = "KB",
        ["KbSearch"] = "KB",
        ["KbMatterPins"] = "KB",
        ["Reports"] = "Reports",
        ["Users"] = "UsersAdmin",
        ["Roles"] = "UsersAdmin",
        ["Permissions"] = "UsersAdmin",
        ["Teams"] = "UsersAdmin",
        ["Departments"] = "UsersAdmin",
        ["Branches"] = "UsersAdmin",
        ["Sessions"] = "UsersAdmin",
        ["LoginHistory"] = "UsersAdmin",
        ["Settings"] = "Settings",
        ["NumberSeries"] = "Settings",
        ["TaxRates"] = "Settings",
        ["Templates"] = "Settings",
        ["WorkflowRules"] = "Settings",
        ["Ai"] = "AI",
        ["Notifications"] = "Notifications",
        ["Webhooks"] = "Webhooks",
        ["PortalAuth"] = "Portal",
        ["PortalMatters"] = "Portal",
        ["PortalAppointments"] = "Portal",
        ["PortalDocuments"] = "Portal",
        ["PortalInvoices"] = "Portal",
        ["PortalMessages"] = "Portal",
    };

    /// <summary>Falls back to the raw controller name for any controller added after this map was last updated, rather than throwing — an uncovered controller still gets a (less pretty) tag instead of breaking doc generation.</summary>
    public static IList<string> Resolve(ApiDescription apiDescription)
    {
        var controllerName = (apiDescription.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;
        if (controllerName is null)
        {
            return ["Other"];
        }

        return [AreaByController.TryGetValue(controllerName, out var area) ? area : controllerName];
    }
}
