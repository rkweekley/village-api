using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class FamilyModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/families").RequireAuthorization();

        // GET /api/families/mine — current user's family
        group.MapGet("/mine", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var family = await db.Families
                .Include(f => f.Members)
                .FirstOrDefaultAsync(f => f.Id == familyId.Value, ct);

            if (family == null) return Results.NotFound();

            return Results.Ok(new
            {
                family.Id,
                family.Name,
                family.InviteCode,
                family.CurrencyName,
                family.Timezone,
                Members = family.Members.Select(m => new
                {
                    m.Id,
                    m.DisplayName,
                    m.Email,
                    Role = m.Role.ToString(),
                    m.PointsBalance,
                    m.BirthDate
                })
            });
        })
        .WithDescription("Get the current user's family with all members.");

        // PATCH /api/families/mine — update family settings
        group.MapPatch("/mine", async (
            UpdateFamilyRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            if (request.Name != null) family.Name = request.Name;
            if (request.CurrencyName != null) family.CurrencyName = request.CurrencyName;
            if (request.Timezone != null) family.Timezone = request.Timezone;
            family.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { family.Id, family.Name, family.CurrencyName, family.Timezone });
        })
        .WithDescription("Update family name, currency name, or timezone.");

        // GET /api/families/invite/{code} — look up invite code
        group.MapGet("/invite/{code}", async (
            string code,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var family = await db.Families
                .FirstOrDefaultAsync(f => f.InviteCode == code.ToUpperInvariant(), ct);

            if (family == null) return Results.NotFound(new { error = "Invalid invite code" });

            return Results.Ok(new
            {
                family.Id,
                family.Name,
                family.InviteCode,
                memberCount = await db.Users.CountAsync(u => u.FamilyId == family.Id, ct)
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("InviteLookup")
        .WithDescription("Look up a family by invite code (used during registration).");
    }
}

public record UpdateFamilyRequest(
    string? Name,
    string? CurrencyName,
    string? Timezone
);
