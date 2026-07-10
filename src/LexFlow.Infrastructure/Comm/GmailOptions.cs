namespace LexFlow.Infrastructure.Comm;

/// <summary>Gmail API OAuth2 + API config (Module 11). Bound from configuration section "Gmail".</summary>
public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://gmail.googleapis.com";
    public string AuthorizeUrl { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
}
