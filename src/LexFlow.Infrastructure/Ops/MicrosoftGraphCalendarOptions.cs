namespace LexFlow.Infrastructure.Ops;

/// <summary>Microsoft Graph (Microsoft 365) calendar OAuth2 + API config (Module 6). Bound from configuration section "MicrosoftGraphCalendar".</summary>
public sealed class MicrosoftGraphCalendarOptions
{
    public const string SectionName = "MicrosoftGraphCalendar";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
    public string AuthorizeUrl { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    public string TokenUrl { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
}
