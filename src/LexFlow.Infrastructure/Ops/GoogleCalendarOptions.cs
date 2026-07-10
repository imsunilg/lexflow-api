namespace LexFlow.Infrastructure.Ops;

/// <summary>Google Calendar OAuth2 + API config (Module 6). Bound from configuration section "GoogleCalendar".</summary>
public sealed class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://www.googleapis.com";
    public string AuthorizeUrl { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
}
