using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Village.Api.Services.Billing;

/// <summary>A parsed Real-time Developer Notification (Cloud Pub/Sub push).</summary>
public sealed record GoogleRtdn(string? PurchaseToken, int NotificationType, string Kind);

/// <summary>
/// Reads authoritative subscription state from the Google Play Developer API
/// (subscriptionsv2.get) and parses Real-time Developer Notifications.
///
/// Authenticates with a GCP service account: RS256-signed JWT exchanged for an
/// OAuth2 access token. The service account needs the "View financial data" /
/// "Manage orders" permission on the Play Console.
/// </summary>
public sealed class GooglePlayService
{
    private const string AndroidPublisherScope = "https://www.googleapis.com/auth/androidpublisher";

    private readonly ILogger<GooglePlayService> _logger;
    private readonly IHttpClientFactory _http;
    private readonly string? _packageName;
    private readonly string? _serviceAccountJson;

    public GooglePlayService(IConfiguration config, ILogger<GooglePlayService> logger, IHttpClientFactory http)
    {
        _logger = logger;
        _http = http;
        _packageName = Environment.GetEnvironmentVariable("GOOGLE_PACKAGE_NAME") ?? config["Google:PackageName"];
        _serviceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_JSON") ?? config["Google:ServiceAccountJson"];
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_packageName) && !string.IsNullOrWhiteSpace(_serviceAccountJson);

    public string? PackageName => _packageName;

    /// <summary>Fetch authoritative subscription state from the Play Developer API.</summary>
    public async Task<GoogleSubscriptionStatus?> GetSubscriptionAsync(string purchaseToken, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var client = _http.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{_packageName}" +
            $"/purchases/subscriptionsv2/tokens/{Uri.EscapeDataString(purchaseToken)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var state = GetString(root, "subscriptionState") ?? string.Empty;
        string? productId = null;
        DateTime? expiry = null;
        bool? autoRenew = null;

        if (root.TryGetProperty("lineItems", out var lineItems) && lineItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in lineItems.EnumerateArray())
            {
                productId = GetString(item, "productId");
                if (GetString(item, "expiryTime") is { } expStr && DateTimeOffset.TryParse(expStr, out var dto))
                    expiry = dto.UtcDateTime;
                if (item.TryGetProperty("autoRenewingPlan", out var plan)
                    && plan.TryGetProperty("autoRenewEnabled", out var ar)
                    && ar.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    autoRenew = ar.GetBoolean();
                }
                break;
            }
        }

        return new GoogleSubscriptionStatus(state, productId, expiry, autoRenew);
    }

    /// <summary>
    /// Parse a Real-time Developer Notification push body. Returns null if the body is
    /// not a recognizable Pub/Sub push. The purchase token it returns is used only to
    /// locate the family; authoritative state is always re-fetched from the Developer API.
    /// </summary>
    public GoogleRtdn? ParseRtdn(string jsonBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;
            if (!root.TryGetProperty("message", out var message)) return null;

            var dataB64 = GetString(message, "data");
            if (dataB64 == null) return null;

            var inner = Convert.FromBase64String(dataB64);
            using var innerDoc = JsonDocument.Parse(inner);
            var r = innerDoc.RootElement;

            if (r.TryGetProperty("subscriptionNotification", out var sub))
            {
                return new GoogleRtdn(
                    GetString(sub, "purchaseToken"),
                    sub.TryGetProperty("notificationType", out var nt) && nt.ValueKind == JsonValueKind.Number
                        ? nt.GetInt32() : 0,
                    "subscription");
            }
            if (r.TryGetProperty("voidedPurchaseNotification", out var voided))
                return new GoogleRtdn(GetString(voided, "purchaseToken"), 0, "voided");
            if (r.TryGetProperty("testNotification", out _))
                return new GoogleRtdn(null, 0, "test");

            return new GoogleRtdn(null, 0, "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Google RTDN payload");
            return null;
        }
    }

    // ── OAuth ───────────────────────────────────────────────────────

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(_serviceAccountJson!);
        var root = doc.RootElement;
        var clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("Google service account JSON missing client_email.");
        var privateKey = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("Google service account JSON missing private_key.");
        var privateKeyId = root.TryGetProperty("private_key_id", out var kid) ? kid.GetString() : null;
        var tokenUri = root.TryGetProperty("token_uri", out var tu) ? tu.GetString() : null
            ?? "https://oauth2.googleapis.com/token";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);

        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: clientEmail,
            audience: tokenUri,
            claims: new[] { new Claim("scope", AndroidPublisherScope) },
            notBefore: now,
            expires: now.AddMinutes(59),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        if (privateKeyId != null) jwt.Header["kid"] = privateKeyId;

        var assertion = new JwtSecurityTokenHandler().WriteToken(jwt);

        var client = _http.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion,
        });

        using var response = await client.PostAsync(tokenUri, form, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var respDoc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return respDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google OAuth response missing access_token.");
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;
}
