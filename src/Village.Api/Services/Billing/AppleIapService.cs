using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Village.Api.Services.Billing;

/// <summary>
/// Verifies Apple signed payloads (StoreKit 2 transaction JWS + App Store Server
/// Notifications v2) and reads subscription state from the App Store Server API v1.
///
/// JWS verification anchors trust to Apple's root CAs (downloaded once and cached)
/// via the x5c certificate chain, so forged payloads are rejected cryptographically.
/// </summary>
public sealed class AppleIapService
{
    private const string ConnectApiAudience = "appstoreconnect-v1";

    private static readonly object RootLock = new();
    private static IReadOnlyList<X509Certificate2>? _cachedRoots;
    private static DateTime _rootsLoadedAt = DateTime.MinValue;

    private readonly ILogger<AppleIapService> _logger;
    private readonly IHttpClientFactory _http;
    private readonly string? _issuerId;
    private readonly string? _keyId;
    private readonly string? _bundleId;
    private readonly string? _privateKeyPem;
    private readonly string _defaultEnvironment;
    private readonly Lazy<ECDsa?> _privateKey;

    public AppleIapService(IConfiguration config, ILogger<AppleIapService> logger, IHttpClientFactory http)
    {
        _logger = logger;
        _http = http;
        _issuerId = Environment.GetEnvironmentVariable("APPLE_ISSUER_ID") ?? config["Apple:IssuerId"];
        _keyId = Environment.GetEnvironmentVariable("APPLE_KEY_ID") ?? config["Apple:KeyId"];
        _bundleId = Environment.GetEnvironmentVariable("APPLE_BUNDLE_ID") ?? config["Apple:BundleId"];
        _privateKeyPem = Environment.GetEnvironmentVariable("APPLE_PRIVATE_KEY") ?? config["Apple:PrivateKey"];
        _defaultEnvironment = config["Apple:Environment"] ?? "Production";
        _privateKey = new Lazy<ECDsa?>(() =>
            string.IsNullOrWhiteSpace(_privateKeyPem) ? null : LoadEcdsaKey(_privateKeyPem!));
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_issuerId)
        && !string.IsNullOrWhiteSpace(_keyId)
        && !string.IsNullOrWhiteSpace(_bundleId)
        && !string.IsNullOrWhiteSpace(_privateKeyPem);

    // ── Verification ────────────────────────────────────────────────

    public async Task<AppleTransaction> VerifyTransactionAsync(string jws, CancellationToken ct)
    {
        var token = await ValidateAppleJwsAsync(jws, ct);
        return ParseTransaction(ParsePayload(token));
    }

    public async Task<AppleNotification> VerifyNotificationAsync(string signedPayload, CancellationToken ct)
    {
        var outer = await ValidateAppleJwsAsync(signedPayload, ct);
        var body = ParsePayload(outer);

        var notificationType = GetString(body, "notificationType")
            ?? throw new SecurityTokenException("Apple notification missing notificationType");
        var subtype = GetString(body, "subtype");

        AppleTransaction? tx = null;
        AppleRenewalInfo? renewal = null;

        if (body.TryGetProperty("data", out var data))
        {
            if (GetString(data, "signedTransactionInfo") is { } txJws)
            {
                var txToken = await ValidateAppleJwsAsync(txJws, ct);
                tx = ParseTransaction(ParsePayload(txToken));
            }
            if (GetString(data, "signedRenewalInfo") is { } renewalJws)
            {
                var renewalToken = await ValidateAppleJwsAsync(renewalJws, ct);
                renewal = ParseRenewal(ParsePayload(renewalToken));
            }
        }

        return new AppleNotification(notificationType, subtype, tx, renewal);
    }

    // ── App Store Server API v1 ─────────────────────────────────────

    public async Task<AppleSubscriptionStatus?> GetSubscriptionStatusAsync(
        string originalTransactionId, string? environment, CancellationToken ct)
    {
        var clientSecret = GenerateClientSecret();
        var baseUrl = (environment ?? _defaultEnvironment).Equals("Sandbox", StringComparison.OrdinalIgnoreCase)
            ? "https://api.storekit-sandbox.itunes.apple.com"
            : "https://api.storekit.itunes.apple.com";

        var client = _http.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/inApps/v1/subscriptions/{Uri.EscapeDataString(originalTransactionId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientSecret);

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var env = GetString(root, "environment") ?? environment ?? string.Empty;
        int status = 0;
        AppleTransaction? tx = null;
        AppleRenewalInfo? renewal = null;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in data.EnumerateArray())
            {
                if (!group.TryGetProperty("lastTransactions", out var last) || last.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var lt in last.EnumerateArray())
                {
                    status = lt.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number
                        ? st.GetInt32() : 0;
                    if (GetString(lt, "signedTransactionInfo") is { } txJws)
                    {
                        var t = await ValidateAppleJwsAsync(txJws, ct);
                        tx = ParseTransaction(ParsePayload(t));
                    }
                    if (GetString(lt, "signedRenewalInfo") is { } renewalJws)
                    {
                        var t = await ValidateAppleJwsAsync(renewalJws, ct);
                        renewal = ParseRenewal(ParsePayload(t));
                    }
                    break;
                }
                break;
            }
        }

        return new AppleSubscriptionStatus(env, status, tx, renewal);
    }

    // ── Server-to-server auth ───────────────────────────────────────

    public string GenerateClientSecret()
    {
        var key = _privateKey.Value
            ?? throw new InvalidOperationException("Apple In-App Purchase private key (.p8) is not configured.");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuerId,
            Audience = ConnectApiAudience,
            IssuedAt = DateTime.UtcNow.AddSeconds(-5),
            NotBefore = DateTime.UtcNow.AddSeconds(-5),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256),
            Claims = new Dictionary<string, object> { ["bid"] = _bundleId! },
            AdditionalHeaderClaims = new Dictionary<string, object> { ["kid"] = _keyId! },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // ── JWS plumbing ────────────────────────────────────────────────

    private async Task<JsonWebToken> ValidateAppleJwsAsync(string jws, CancellationToken ct)
    {
        var handler = new JsonWebTokenHandler();
        var token = handler.ReadJsonWebToken(jws)
            ?? throw new SecurityTokenException("Apple signed payload is not a valid JWS.");

        var x5c = GetX5cChain(token);
        if (x5c.Length == 0)
            throw new SecurityTokenException("Apple signed payload is missing the x5c certificate chain.");

        var leaf = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(x5c[0]));
        using var ecdsa = leaf.GetECDsaPublicKey()
            ?? throw new SecurityTokenException("Apple leaf certificate does not contain an ECDSA public key.");

        var validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new ECDsaSecurityKey(ecdsa),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false, // Apple payloads carry no exp; they carry signedDate/expiresDate
            ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },
        };

        var result = await handler.ValidateTokenAsync(jws, validationParameters);
        if (!result.IsValid)
            throw new SecurityTokenException(
                $"Apple signed payload signature is invalid: {result.Exception?.Message}");

        ValidateChain(leaf, x5c, GetAppleRoots(_logger));
        return token;
    }

    private static string[] GetX5cChain(JsonWebToken token)
    {
        var headerBytes = Base64UrlEncoder.DecodeBytes(token.EncodedHeader);
        using var doc = JsonDocument.Parse(headerBytes);
        if (!doc.RootElement.TryGetProperty("x5c", out var x5c) || x5c.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return x5c.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToArray();
    }

    private static void ValidateChain(
        X509Certificate2 leaf, string[] x5c, IReadOnlyList<X509Certificate2> roots)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Apple does not publish CRL/OCSP for these
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.CustomTrustStore.Clear();
        foreach (var root in roots) chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.ExtraStore.Clear();
        for (var i = 1; i < x5c.Length; i++)
            chain.ChainPolicy.ExtraStore.Add(X509CertificateLoader.LoadCertificate(Convert.FromBase64String(x5c[i])));

        if (!chain.Build(leaf))
        {
            var reasons = string.Join("; ", chain.ChainStatus.Select(s => $"{s.Status}:{s.StatusInformation}"));
            throw new SecurityTokenException($"Apple certificate chain validation failed: {reasons}");
        }
    }

    private static IReadOnlyList<X509Certificate2> GetAppleRoots(ILogger logger)
    {
        lock (RootLock)
        {
            if (_cachedRoots != null && DateTime.UtcNow - _rootsLoadedAt < TimeSpan.FromHours(24))
                return _cachedRoots;

            var urls = new[]
            {
                "https://www.apple.com/certificateauthority/AppleRootCA-G1.cer",
                "https://www.apple.com/certificateauthority/AppleRootCA-G2.cer",
                "https://www.apple.com/certificateauthority/AppleRootCA-G3.cer",
            };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var certs = new List<X509Certificate2>();
            foreach (var url in urls)
            {
                try
                {
                    var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    certs.Add(X509CertificateLoader.LoadCertificate(bytes));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to download Apple root CA from {Url}", url);
                }
            }

            if (certs.Count == 0)
                throw new InvalidOperationException(
                    "Unable to obtain Apple root CA certificates; cannot verify Apple signed payloads.");

            _cachedRoots = certs;
            _rootsLoadedAt = DateTime.UtcNow;
            return certs;
        }
    }

    // ── Parsing ─────────────────────────────────────────────────────

    private static JsonElement ParsePayload(JsonWebToken token)
    {
        var payloadBytes = Base64UrlEncoder.DecodeBytes(token.EncodedPayload);
        var doc = JsonDocument.Parse(payloadBytes);
        return doc.RootElement.Clone();
    }

    private static AppleTransaction ParseTransaction(JsonElement e) => new(
        GetString(e, "originalTransactionId")
            ?? throw new SecurityTokenException("Apple transaction is missing originalTransactionId."),
        GetString(e, "transactionId") ?? string.Empty,
        GetString(e, "productId") ?? string.Empty,
        GetDateMs(e, "expiresDate"),
        GetString(e, "environment"),
        GetString(e, "appAccountToken"),
        GetString(e, "inAppOwnershipType"));

    private static AppleRenewalInfo ParseRenewal(JsonElement e) => new(
        GetString(e, "originalTransactionId") ?? string.Empty,
        GetString(e, "productId") ?? string.Empty,
        e.TryGetProperty("autoRenewStatus", out var ars) && ars.ValueKind == JsonValueKind.Number ? ars.GetInt32() : 0,
        e.TryGetProperty("expirationIntent", out var ei) && ei.ValueKind == JsonValueKind.Number ? ei.GetInt32() : 0,
        GetDateMs(e, "gracePeriodExpiresDate"));

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetDateMs(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeMilliseconds(v.GetInt64()).UtcDateTime
            : null;

    private static ECDsa LoadEcdsaKey(string pem)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa;
    }
}
