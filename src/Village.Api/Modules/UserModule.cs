using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Dtos.Auth;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class UserModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        // ── Profile Update ───────────────────────────────────
        group.MapPut("/me", async (
            HttpContext http,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await http.Request.ReadFromJsonAsync<UpdateProfileRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var userId = http.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Results.NotFound();

            // Validate at least one field is provided
            if (request.DisplayName == null && request.Email == null && request.BirthDate == null)
                return Results.BadRequest(new { error = "At least one field must be provided." });

            // Update display name
            if (request.DisplayName != null)
            {
                if (string.IsNullOrWhiteSpace(request.DisplayName))
                    return Results.BadRequest(new { error = "Display name cannot be empty." });
                user.DisplayName = request.DisplayName.Trim();
            }

            // Update email (check uniqueness)
            if (request.Email != null)
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return Results.BadRequest(new { error = "Email cannot be empty." });
                var normalized = request.Email.Trim().ToLowerInvariant();
                var existingUser = await db.Users
                    .FirstOrDefaultAsync(u => u.Email == normalized && u.Id != userId.Value, ct);
                if (existingUser != null)
                    return Results.Conflict(new { error = "Email is already in use by another account." });
                user.Email = normalized;
            }

            // Update birth date
            if (request.BirthDate != null)
                user.BirthDate = request.BirthDate;

            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

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
        .WithDescription("Update the current user's profile (display name, email, birth date).");

        // ── Data Export (GDPR right-to-access) ──────────────────
        group.MapGet("/me/export", async (
            HttpContext http,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Results.NotFound();

            var choreAssignments = await db.ChoreAssignments
                .Where(a => a.AssignedToId == userId.Value)
                .Select(a => new
                {
                    a.Id,
                    a.ChoreId,
                    Status = a.Status.ToString(),
                    a.DueDate,
                    a.CompletedAt
                })
                .ToListAsync(ct);

            var points = await db.PointsTransactions
                .Where(p => p.UserId == userId.Value)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.BalanceAfter,
                    Type = p.Type.ToString(),
                    p.Note,
                    p.CreatedAt
                })
                .ToListAsync(ct);

            var rewardRedemptions = await db.RewardRedemptions
                .Where(r => r.UserId == userId.Value)
                .Select(r => new
                {
                    r.Id,
                    r.RewardId,
                    Status = r.Status.ToString(),
                    PointsSpent = r.PointsCost,
                    r.CreatedAt,
                    r.ApprovedAt
                })
                .ToListAsync(ct);

            var calendarAttendance = await db.CalendarEventAttendees
                .Include(a => a.Event)
                .Where(a => a.UserId == userId.Value)
                .Select(a => new
                {
                    a.EventId,
                    EventTitle = a.Event.Title,
                    Status = a.Status.ToString()
                })
                .ToListAsync(ct);

            var schoolWork = await db.SchoolWorks
                .Include(w => w.Subject)
                .Where(w => w.AssignedToId == userId.Value)
                .Select(w => new
                {
                    w.Id,
                    Subject = w.Subject.Name,
                    w.Title,
                    Status = w.Status.ToString(),
                    w.PointsEarned,
                    w.PointsPossible,
                    w.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                user = new
                {
                    user.Id,
                    user.Email,
                    user.DisplayName,
                    Role = user.Role.ToString(),
                    user.PointsBalance,
                    user.CreatedAt
                },
                choreAssignments,
                points,
                rewardRedemptions,
                calendarAttendance,
                schoolWork,
                exportedAt = DateTime.UtcNow
            });
        })
        .WithDescription("Export all personal data (GDPR right-to-access).");

        // ── Account Deactivation (GDPR right-to-delete) ─────────
        group.MapPost("/me/deactivate", async (
            HttpContext http,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Results.NotFound();

            // Anonymize: remove PII but keep FK integrity
            user.Email = $"deleted_{userId}@anonymous.invalid";
            user.DisplayName = "Deleted User";
            user.PasswordHash = "";
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Account deactivated. Data has been anonymized." });
        })
        .WithDescription("Deactivate account and anonymize personal data (GDPR right-to-delete).");
    }
}

// ── Request DTO ──

public record UpdateProfileRequest(
    string? DisplayName,
    string? Email,
    DateOnly? BirthDate
);
