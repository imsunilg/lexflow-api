using Json.Schema;

namespace LexFlow.Infrastructure.Settings;

/// <summary>
/// One JSON Schema per Module 15 section (PRD Module 15 Validation: "Section
/// JSON-schema validated"). Covers the 7 sections with no dedicated typed table
/// (firm_details, branding, theme, document_templates, business_hours, data,
/// security) plus the 4 gateway-backed sections (smtp, sms_gateway, whatsapp,
/// payment_gateways), whose non-secret config_json is validated the same way before
/// being written to core.gateway_configs. The 4 collection-shaped sections (taxes,
/// number_series, email_templates, workflow_rules) are validated per-item by their
/// own dedicated services instead (ITaxRateService etc.), not here.
/// </summary>
public static class SettingsSchemaProvider
{
    private static readonly Dictionary<string, JsonSchema> Schemas = new()
    {
        ["firm_details"] = JsonSchema.FromText("""
            {
              "type": "object",
              "required": ["legalName", "fiscalYearStartMonth", "locale", "currency", "timezone"],
              "properties": {
                "legalName": { "type": "string", "minLength": 1 },
                "displayName": { "type": "string" },
                "registrationNumbers": { "type": "object" },
                "gstinOrPan": { "type": "string" },
                "fiscalYearStartMonth": { "type": "integer", "minimum": 1, "maximum": 12 },
                "locale": { "type": "string" },
                "currency": { "type": "string" },
                "timezone": { "type": "string" }
              }
            }
            """),
        ["branding"] = JsonSchema.FromText("""
            {
              "type": "object",
              "properties": {
                "logoLightUrl": { "type": "string" },
                "logoDarkUrl": { "type": "string" },
                "primaryColor": { "type": "string", "pattern": "^#[0-9a-fA-F]{6}$" },
                "accentColor": { "type": "string", "pattern": "^#[0-9a-fA-F]{6}$" },
                "letterheadMarginsMm": { "type": "object" },
                "emailFooter": { "type": "string" }
              }
            }
            """),
        ["theme"] = JsonSchema.FromText("""
            {
              "type": "object",
              "required": ["default"],
              "properties": {
                "default": { "type": "string", "enum": ["Light", "Dark", "System"] },
                "allowUserOverride": { "type": "boolean" }
              }
            }
            """),
        ["document_templates"] = JsonSchema.FromText("""
            {
              "type": "object",
              "properties": {
                "templates": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["name", "blobPath"],
                    "properties": {
                      "name": { "type": "string" },
                      "blobPath": { "type": "string" },
                      "version": { "type": "integer" }
                    }
                  }
                }
              }
            }
            """),
        ["business_hours"] = JsonSchema.FromText("""
            {
              "type": "object",
              "properties": {
                "weeklyHours": { "type": "object" },
                "holidays": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["date", "name"],
                    "properties": {
                      "date": { "type": "string", "format": "date" },
                      "name": { "type": "string" },
                      "branchId": { "type": ["string", "null"] }
                    }
                  }
                }
              }
            }
            """),
        ["data"] = JsonSchema.FromText("""
            {
              "type": "object",
              "properties": {
                "retentionYears": { "type": "integer", "minimum": 1 },
                "legalHold": { "type": "boolean" }
              }
            }
            """),
        ["security"] = JsonSchema.FromText("""
            {
              "type": "object",
              "required": ["passwordMinLength", "twoFactorEnforcement", "sessionTimeoutMinutes"],
              "properties": {
                "passwordMinLength": { "type": "integer", "minimum": 10 },
                "twoFactorEnforcement": { "type": "string", "enum": ["off", "optional", "required-per-role"] },
                "sessionTimeoutMinutes": { "type": "integer", "minimum": 1 },
                "ipAllowlistEnabled": { "type": "boolean" }
              }
            }
            """),
        ["smtp"] = JsonSchema.FromText("""
            {
              "type": "object",
              "required": ["host", "port", "tlsMode", "fromAddress"],
              "properties": {
                "host": { "type": "string", "minLength": 1 },
                "port": { "type": "integer", "minimum": 1, "maximum": 65535 },
                "tlsMode": { "type": "string", "enum": ["None", "StartTls", "Ssl", "Tls"] },
                "username": { "type": "string" },
                "fromName": { "type": "string" },
                "fromAddress": { "type": "string", "format": "email" },
                "useFallbackPlatformMailer": { "type": "boolean" }
              }
            }
            """),
        ["sms_gateway"] = JsonSchema.FromText("""
            {
              "type": "object",
              "required": ["provider", "senderId"],
              "properties": {
                "provider": { "type": "string", "enum": ["sms_twilio", "sms_msg91"] },
                "accountSid": { "type": "string" },
                "senderId": { "type": "string", "minLength": 1 },
                "dltEntityId": { "type": "string" }
              }
            }
            """),
        ["whatsapp"] = JsonSchema.FromText("""
            {
              "type": "object",
              "required": ["wabaId", "phoneNumberId"],
              "properties": {
                "wabaId": { "type": "string", "minLength": 1 },
                "phoneNumberId": { "type": "string", "minLength": 1 }
              }
            }
            """),
        ["payment_gateways"] = JsonSchema.FromText("""
            {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["provider"],
                "properties": {
                  "provider": { "type": "string", "enum": ["stripe", "razorpay", "paypal"] },
                  "publishableKey": { "type": "string" },
                  "keyId": { "type": "string" },
                  "clientId": { "type": "string" },
                  "webhookSecretRef": { "type": "string" }
                }
              }
            }
            """),
    };

    /// <summary>Sections read/written as a single JSON blob through the generic GET/PUT /settings/{section} endpoint.</summary>
    public static readonly IReadOnlySet<string> BlobSections = new HashSet<string>
    {
        "firm_details", "branding", "theme", "document_templates", "business_hours", "data", "security",
    };

    /// <summary>Sections backed by core.gateway_configs (non-secret fields only).</summary>
    public static readonly IReadOnlySet<string> GatewaySections = new HashSet<string> { "smtp", "sms_gateway", "whatsapp", "payment_gateways" };

    /// <summary>Sections that are read-only through the generic endpoint — mutate via their own dedicated CRUD endpoints instead.</summary>
    public static readonly IReadOnlySet<string> CollectionSections = new HashSet<string> { "taxes", "number_series", "email_templates", "workflow_rules" };

    public static bool TryGetSchema(string section, out JsonSchema schema) => Schemas.TryGetValue(section, out schema!);
}
