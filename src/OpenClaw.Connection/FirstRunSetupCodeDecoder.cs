using System.Net;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Connection;

/// <summary>
/// Validates short-lived DYNAM first-run codes before the general setup-code connection path.
/// This intentionally accepts only a public secure gateway URL and a bootstrap credential.
/// </summary>
public static class FirstRunSetupCodeDecoder
{
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(11);

    public sealed record DecodeResult(
        bool Success,
        string? Url = null,
        string? BootstrapToken = null,
        DateTimeOffset? ExpiresAt = null,
        string? Error = null);

    public static DecodeResult Decode(string setupCode, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(setupCode) || setupCode.Length > 2048)
            return Invalid("The setup code is empty or too long.");

        string json;
        try
        {
            var encoded = setupCode.Trim().Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return Invalid("The setup code is not valid.");
        }

        if (json.Length > 4096)
            return Invalid("The setup code is too long.");

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Invalid("The setup code is not valid.");

            string? url = null;
            string? bootstrapToken = null;
            long? expiresAtMs = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                    return Invalid("The setup code is not valid.");

                switch (property.Name)
                {
                    case "url" when property.Value.ValueKind == JsonValueKind.String:
                        url = property.Value.GetString();
                        break;
                    case "bootstrapToken" when property.Value.ValueKind == JsonValueKind.String:
                        bootstrapToken = property.Value.GetString();
                        break;
                    case "expiresAtMs" when property.Value.ValueKind == JsonValueKind.Number &&
                                             property.Value.TryGetInt64(out var timestamp):
                        expiresAtMs = timestamp;
                        break;
                    default:
                        return Invalid("The setup code contains unsupported fields.");
                }
            }

            if (seen.Count != 3 || string.IsNullOrWhiteSpace(url) ||
                string.IsNullOrWhiteSpace(bootstrapToken) || bootstrapToken.Length > 512 ||
                expiresAtMs is null)
            {
                return Invalid("The setup code is missing required information.");
            }

            if (!IsPublicSecureGatewayUrl(url, out var normalizedUrl))
                return Invalid("The setup code must use a public secure gateway address.");

            DateTimeOffset expiresAt;
            try
            {
                expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtMs.Value);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Invalid("The setup code expiry is invalid.");
            }

            var currentTime = now ?? DateTimeOffset.UtcNow;
            if (expiresAt <= currentTime)
                return Invalid("The setup code has expired. Ask your agent for a new one.");
            if (expiresAt > currentTime + MaximumLifetime)
                return Invalid("The setup code expiry is invalid. Ask your agent for a new one.");

            return new DecodeResult(true, normalizedUrl, bootstrapToken, expiresAt);
        }
        catch (JsonException)
        {
            return Invalid("The setup code is not valid.");
        }
    }

    private static bool IsPublicSecureGatewayUrl(string value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ||
            uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            IPAddress.TryParse(uri.Host, out _) || Uri.CheckHostName(uri.Host) != UriHostNameType.Dns)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        var lowerHost = host.ToLowerInvariant();
        if (!lowerHost.Contains('.') ||
            lowerHost is "localhost" or "host.docker.internal" or "gateway.docker.internal" ||
            lowerHost.EndsWith(".localhost", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".local", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".internal", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".docker.internal", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".home.arpa", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".home", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".lan", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".corp", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".intranet", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".private", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".localdomain", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".test", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".invalid", StringComparison.Ordinal) ||
            lowerHost.EndsWith(".example", StringComparison.Ordinal))
        {
            return false;
        }

        normalizedUrl = uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped).TrimEnd('/');
        return true;
    }

    private static DecodeResult Invalid(string error) => new(false, Error: error);
}
