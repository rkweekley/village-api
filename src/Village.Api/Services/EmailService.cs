using System.Net.Http.Headers;

namespace Village.Api.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string email, string displayName, string resetToken);
    Task SendInviteEmailAsync(string email, string familyName, string inviteCode);
    Task SendWelcomeEmailAsync(string email, string displayName, string familyName);
    Task SendNewSignupAlertAsync(string email, string displayName, string familyName);
}

public class MailgunEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string? _domain;
    private readonly ILogger<MailgunEmailService> _logger;

    private bool IsConfigured => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_domain);

    public MailgunEmailService(HttpClient http, IConfiguration configuration, ILogger<MailgunEmailService> logger)
    {
        _http = http;
        _apiKey = Environment.GetEnvironmentVariable("MAILGUN_API_KEY") 
                  ?? configuration["Mailgun:ApiKey"];
        _domain = Environment.GetEnvironmentVariable("MAILGUN_DOMAIN")
                  ?? configuration["Mailgun:Domain"];
        _logger = logger;

        if (!IsConfigured)
            _logger.LogWarning("Mailgun email service is not configured. Set MAILGUN_API_KEY and MAILGUN_DOMAIN to enable emails.");
    }

    public async Task SendPasswordResetEmailAsync(string email, string displayName, string resetToken)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot send password reset email — Mailgun not configured. Token for {Email}", email);
            return;
        }

        var resetUrl = $"https://villagefamily.app/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(email)}";
        var html = $"<h2>Reset your Village password</h2><p>Hi {displayName},</p><p>Someone requested a password reset. Click below to reset:</p><p><a href=\"{resetUrl}\">Reset Password</a></p><p>This link expires in 1 hour. If you didn't request this, ignore this email.</p>";
        await SendEmailAsync(email, "Reset your Village password", html);
    }

    public async Task SendInviteEmailAsync(string email, string familyName, string inviteCode)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot send invite email — Mailgun not configured. Invite code {Code} for {Family}", inviteCode, familyName);
            return;
        }

        var joinUrl = $"https://villagefamily.app/join?code={inviteCode}";
        var html = $"<h2>Join {familyName} on Village</h2><p>You've been invited to join a family on Village — the family productivity app. Click below to accept:</p><p><a href=\"{joinUrl}\">Join {familyName}</a></p><p>Your invite code: <strong>{inviteCode}</strong></p>";
        await SendEmailAsync(email, $"Join {familyName} on Village", html);
    }

    public async Task SendWelcomeEmailAsync(string email, string displayName, string familyName)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot send welcome email — Mailgun not configured for {Email}", email);
            return;
        }

        var html = $"<h2>Welcome to Village, {displayName}!</h2>" +
                   $"<p>Your family <strong>{familyName}</strong> is all set up.</p>" +
                   "<p>Village helps your family stay organized with chores, rewards, meal planning, " +
                   "shopping lists, and more — all in one place.</p>" +
                   "<p><strong>Here are a few things to get started:</strong></p>" +
                   "<ul>" +
                   "<li>Add your family members under the Family tab</li>" +
                   "<li>Create your first chore and assign it to someone</li>" +
                   "<li>Set up rewards to motivate the kids</li>" +
                   "<li>Plan meals for the week</li>" +
                   "</ul>" +
                   "<p>If you have any questions, just reply to this email — we'd love to hear from you.</p>" +
                   "<p>Welcome to the Village family!</p>";
        await SendEmailAsync(email, $"Welcome to Village, {displayName}!", html);
    }

    public async Task SendNewSignupAlertAsync(string email, string displayName, string familyName)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot send signup alert — Mailgun not configured");
            return;
        }

        var html = $"<h3>New Village Signup</h3>" +
                   $"<p><strong>{displayName}</strong> ({email}) just created the family <strong>{familyName}</strong>.</p>" +
                   "<p><em>— Village Bot</em></p>";
        await SendEmailAsync("info@cyberalsolutions.com", $"New signup: {displayName} — {familyName}", html);
    }

    private async Task SendEmailAsync(string to, string subject, string html)
    {
        var fromAddress = $"Village <noreply@{_domain}>";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["from"] = fromAddress,
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
