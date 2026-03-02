using Microsoft.AspNetCore.Http;

namespace PppPricing.API.Configuration;

public sealed class GoogleOAuthSettingsResolver
{
    private readonly IConfiguration _configuration;

    public GoogleOAuthSettingsResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? GetClientId() =>
        GetFirstNonEmpty("Google:ClientId", "Google__ClientId", "GOOGLE_CLIENT_ID");

    public string? GetClientSecret() =>
        GetFirstNonEmpty("Google:ClientSecret", "Google__ClientSecret", "GOOGLE_CLIENT_SECRET");

    public string GetRedirectUri(HttpRequest request) =>
        GetFirstNonEmpty("Google:RedirectUri", "Google__RedirectUri", "GOOGLE_REDIRECT_URI")
            ?? $"{request.Scheme}://{request.Host}/api/google-play/auth/callback";

    private string? GetFirstNonEmpty(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
