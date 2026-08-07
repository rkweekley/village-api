using System.Security.Cryptography;
using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Dtos.Auth;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;
using Village.Api.Services;

namespace Village.Api.Modules;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // ── Register ──────────────────────────────────────────────
        group.MapPost("/register", async (
            HttpContext httpContext,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<RegisterRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            if (await db.Users.AnyAsync(u => u.Email == request.Email, ct))
                return Results.Conflict(new { error = "Email already registered" });

            bool isNewFamily = string.IsNullOrWhiteSpace(request.InviteCode);
            Family family;

            if (isNewFamily)
            {
                family = new Family
                {
                    Id = Guid.NewGuid(),
                    Name = $"{request.DisplayName}'s Family",
                    InviteCode = GenerateInviteCode()
                };
                db.Families.Add(family);
            }
            else
            {
                var existing = await db.Families
                    .FirstOrDefaultAsync(f => f.InviteCode == request.InviteCode.Trim(), ct);
                if (existing == null)
                    return Results.BadRequest(new { error = "Invalid invite code" });
                family = existing;
            }

            var refreshToken = jwt.GenerateRefreshToken();
            var user = new User
            {
                Id = Guid.NewGuid(),
                FamilyId = family.Id,
                Email = request.Email.ToLowerInvariant().Trim(),
                DisplayName = request.DisplayName.Trim(),
                Role = isNewFamily ? UserRole.Parent : UserRole.Child,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);

            await db.SaveChangesAsync(ct);

            // Fire-and-forget: send welcome email + admin notification
            var emailService = httpContext.RequestServices.GetService<IEmailService>();
            if (emailService != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await emailService.SendWelcomeEmailAsync(user.Email, user.DisplayName, family.Name);
                    }
                    catch (Exception)
                    {
                        // Don't fail registration if email fails
                    }
                });
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await emailService.SendNewSignupAlertAsync(user.Email, user.DisplayName, family.Name);
                    }
                    catch (Exception)
                    {
                        // Don't fail registration if email fails
                    }
                });
            }

            return Results.Created($"/api/users/{user.Id}", new AuthResponse(
                AccessToken: jwt.GenerateAccessToken(user),
                RefreshToken: refreshToken,
                UserId: user.Id,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: user.Role.ToString(),
                FamilyId: family.Id,
                FamilyName: family.Name,
                IsNewFamily: isNewFamily
            ));
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Register a new user.");

        // ── Login ─────────────────────────────────────────────────
        group.MapPost("/login", async (
            HttpContext httpContext,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<LoginRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var user = await db.Users
                .Include(u => u.Family)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            var refreshToken = jwt.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new AuthResponse(
                AccessToken: jwt.GenerateAccessToken(user),
                RefreshToken: refreshToken,
                UserId: user.Id,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: user.Role.ToString(),
                FamilyId: user.FamilyId,
                FamilyName: user.Family.Name,
                IsNewFamily: false
            ));
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Authenticate with email and password.");

        // ── Refresh ───────────────────────────────────────────────
        group.MapPost("/refresh", async (
            HttpContext httpContext,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<RefreshRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var userIdStr = jwt.GetUserIdFromExpiredToken(request.AccessToken);
            if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.RefreshToken == request.RefreshToken &&
                u.RefreshTokenExpiresAt > DateTime.UtcNow, ct);

            if (user == null)
                return Results.Unauthorized();

            var newRefreshToken = jwt.GenerateRefreshToken();
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                accessToken = jwt.GenerateAccessToken(user),
                refreshToken = newRefreshToken
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Refresh an expired access token.");

        // ── Logout ────────────────────────────────────────────────
        group.MapPost("/logout", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiresAt = null;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { message = "Logged out" });
        })
        .RequireAuthorization()
        .WithDescription("Invalidate the current refresh token.");

        // ── Forgot Password ──────────────────────────────────────
        group.MapPost("/forgot-password", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ForgotPasswordRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLowerInvariant().Trim(), ct);
            if (user == null)
                return Results.Ok(new { message = "If the email exists, a reset link has been sent." });

            var token = GenerateResetToken();
            user.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(token);
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync(ct);

            var emailService = httpContext.RequestServices.GetService<IEmailService>();
            if (emailService != null)
            {
                try
                {
                    await emailService.SendPasswordResetEmailAsync(user.Email, user.DisplayName, token);
                }
                catch { }
            }

            return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Request a password reset email.");

        // ── Reset Password ───────────────────────────────────────
        group.MapPost("/reset-password", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ResetPasswordRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            // Look up by email (from the reset link) instead of scanning all users
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLowerInvariant().Trim()
                && u.PasswordResetToken != null
                && u.PasswordResetTokenExpiresAt > DateTime.UtcNow, ct);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Token, user.PasswordResetToken))
                return Results.BadRequest(new { error = "Invalid or expired reset token" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Password reset successfully. Please log in." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Reset password using a token from email.");

        // ── Me ───────────────────────────────────────────────────
        group.MapGet("/me", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null)
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null)
                return Results.NotFound();

            return Results.Ok(new UserInfoResponse(
                Id: user.Id,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: user.Role.ToString(),
                PointsBalance: user.PointsBalance,
                BirthDate: user.BirthDate,
                FamilyId: user.FamilyId
            ));
        })
        .RequireAuthorization()
        .WithDescription("Get the current user's profile.");

        // ── Contact ────────────────────────────────────────────────
        group.MapPost("/contact", async (
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ContactRequest>(ct);
            if (request == null)
                return Results.BadRequest(new { error = "Invalid request body" });

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
                return Results.BadRequest(new { error = "Name is required (max 200 chars)" });
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@') || request.Email.Length > 200)
                return Results.BadRequest(new { error = "Valid email is required" });
            if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200)
                return Results.BadRequest(new { error = "Subject is required (max 200 chars)" });
            if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 5000)
                return Results.BadRequest(new { error = "Message is required (max 5000 chars)" });

            var emailService = httpContext.RequestServices.GetRequiredService<IEmailService>();
            var origin = httpContext.Request.Headers["Origin"].FirstOrDefault() ?? "";

            try
            {
                await emailService.SendContactFormAsync(
                    request.Name.Trim(),
                    request.Email.Trim(),
                    request.Subject.Trim(),
                    request.Message.Trim(),
                    origin);
            }
            catch (Exception)
            {
                // Don't expose Mailgun errors; still return success
            }

            return Results.Ok(new { message = "Message sent! We'll get back to you soon." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Submit a contact form message.");
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static string GenerateResetToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
