using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class UserModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

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
