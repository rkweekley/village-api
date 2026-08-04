using System.Net.Http.Headers;

namespace Village.Api.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string email, string displayName, string resetToken);
    Task SendInviteEmailAsync(string email, string familyName, string inviteCode);
}

public class MailgunEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _domain;
    private readonly string _fromAddress;
    private readonly ILogger<MailgunEmailService> _logger;

    public MailgunEmailService(HttpClient http, IConfiguration configuration, ILogger<MailgunEmailService> logger)
    {
        _http = http;
        _apiKey = Environment.GetEnvironmentVariable("MAILGUN_API_KEY") 
                  ?? configuration["Mailgun:ApiKey"] 
                  ?? throw new InvalidOperationException("MAILGUN_API_KEY not configured");
        _domain = Environment.GetEnvironmentVariable("MAILGUN_DOMAIN")
                  ?? configuration["Mailgun:Domain"]
                  ?? throw new InvalidOperationException("MAILGUN_DOMAIN not configured");
        _fromAddress = $"Village <noreply@{_domain}>";
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string email, string displayName, string resetToken)
    {
        var resetUrl = $"https://app.village.app/reset-password?token={Uri.EscapeDataString(resetToken)}";
        var html = $"<h2>Reset your Village password</h2><p>Hi {displayName},</p><p>Someone requested a password reset. Click below to reset:</p><p><a href=\"{resetUrl}\">Reset Password</a></p><p>This link expires in 1 hour. If you didn't request this, ignore this email.</p>";
        await SendEmailAsync(email, "Reset your Village password", html);
    }

    public async Task SendInviteEmailAsync(string email, string familyName, string inviteCode)
    {
        var joinUrl = $"https://app.village.app/join?code={inviteCode}";
        var html = $"<h2>Join {familyName} on Village</h2><p>You've been invited to join a family on Village — the family productivity app. Click below to accept:</p><p><a href=\"{joinUrl}\">Join {familyName}</a></p><p>Your invite code: <strong>{inviteCode}</strong></p>";
        await SendEmailAsync(email, $"Join {familyName} on Village", html);
    }

    private async Task SendEmailAsync(string to, string subject, string html)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["from"] = _fromAddress,
            ["to"] = to,
            ["subject"] = subject,
            ["html"] = html
        });

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"api:{_apiKey}")));

        var response = await _http.PostAsync($"https://api.mailgun.net/v3/{_domain}/messages", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Mailgun send failed: {StatusCode} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Email send failed: {response.StatusCode}");
        }

        _logger.LogInformation("Email sent to {To} with subject '{Subject}'", to, subject);
    }
}
