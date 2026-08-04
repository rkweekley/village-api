using System.Security.Cryptography;
using Carter;
using Microsoft.AspNetCore.Mvc;
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
            [FromBody] RegisterRequest request,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
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
        .WithDescription("Register a new user. Without invite code → creates a new family as Parent.");

        // ── Login ─────────────────────────────────────────────────
        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            var user = await db.Users
                .Include(u => u.Family)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            // Rotate refresh token on login
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
            [FromBody] RefreshRequest request,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            var userIdStr = jwt.GetUserIdFromExpiredToken(request.AccessToken);
            if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Id == userId &&
                u.RefreshToken == request.RefreshToken &&
                u.RefreshTokenExpiresAt > DateTime.UtcNow, ct);

            if (user == null)
                return Results.Unauthorized();

            // Rotate refresh token
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
        .WithDescription("Refresh an expired access token using a valid refresh token.");

        // ── Logout ────────────────────────────────────────────────
        group.MapPost("/logout", async (
            HttpContext http,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
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
            [FromBody] ForgotPasswordRequest request,
            VillageDbContext db,
            IEmailService? email,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            // Always return same response to prevent email enumeration
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLowerInvariant().Trim(), ct);
            if (user == null)
                return Results.Ok(new { message = "If the email exists, a reset link has been sent." });

            var token = GenerateResetToken();
            user.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(token); // Hash in DB
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync(ct);

            if (email != null)
            {
                try
                {
                    await email.SendPasswordResetEmailAsync(user.Email, user.DisplayName, token);
                }
                catch
                {
                    // Logged inside EmailService; don't expose email failure to attacker
                }
            }

            return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Request a password reset email.");

        // ── Reset Password ───────────────────────────────────────
        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            // Find user with non-expired reset token; verify against hash
            var candidates = await db.Users
                .Where(u => u.PasswordResetToken != null
                            && u.PasswordResetTokenExpiresAt > DateTime.UtcNow)
                .ToListAsync(ct);

            User? found = null;
            foreach (var user in candidates)
            {
                if (BCrypt.Net.BCrypt.Verify(request.Token, user.PasswordResetToken))
                {
                    found = user;
                    break;
                }
            }

            if (found == null)
                return Results.BadRequest(new { error = "Invalid or expired reset token" });

            found.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            found.PasswordResetToken = null;
            found.PasswordResetTokenExpiresAt = null;
            // Invalidate all existing refresh tokens
            found.RefreshToken = null;
            found.RefreshTokenExpiresAt = null;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Password reset successfully. Please log in." });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Auth")
        .WithDescription("Reset password using a token from the forgot-password email.");

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
        .WithDescription("Get the currently authenticated user's profile.");
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private static string GenerateResetToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
