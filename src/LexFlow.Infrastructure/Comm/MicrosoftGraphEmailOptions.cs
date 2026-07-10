namespace LexFlow.Infrastructure.Comm;

/// <summary>Microsoft Graph mail OAuth2 + API config (Module 11). Bound from configuration section "MicrosoftGraphEmail".</summary>
public sealed class MicrosoftGraphEmailOptions
{
    public const string SectionName = "MicrosoftGraphEmail";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
    public string AuthorizeUrl { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    public string TokenUrl { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
}
