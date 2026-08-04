using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
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
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<UpdateFamilyRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

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
        .Accepts<UpdateFamilyRequest>("application/json")
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
        .WithDescription("Look up a family by invite code (used during registration).");

        // PUT /api/families/mine/members/{userId}/role — change member role
        group.MapPut("/mine/members/{userId:guid}/role", async (
            Guid userId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ChangeRoleRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            var currentUserRole = httpContext.User.GetRole();
            if (familyId == null || currentUserRole != "Parent")
                return Results.Forbid();

            var member = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.FamilyId == familyId.Value, ct);
            if (member == null) return Results.NotFound();

            if (!Enum.TryParse<UserRole>(request.Role, true, out var newRole))
                return Results.BadRequest(new { error = "Invalid role. Use: Parent, Child, Caregiver" });

            member.Role = newRole;
            member.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { member.Id, member.DisplayName, Role = member.Role.ToString() });
        })
        .WithDescription("Change a family member's role (Parent only).");

        // DELETE /api/families/mine/members/{userId} — remove member
        group.MapDelete("/mine/members/{userId:guid}", async (
            Guid userId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var currentUserRole = httpContext.User.GetRole();
            if (familyId == null || currentUserRole != "Parent")
                return Results.Forbid();

            var member = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.FamilyId == familyId.Value, ct);
            if (member == null) return Results.NotFound();

            if (member.Role == UserRole.Parent)
            {
                var parentCount = await db.Users
                    .CountAsync(u => u.FamilyId == familyId.Value && u.Role == UserRole.Parent, ct);
                if (parentCount <= 1)
                    return Results.BadRequest(new { error = "Cannot remove the last Parent" });
            }

            // Anonymize: keep FK integrity but remove PII
            member.Email = $"removed_{member.Id}@anonymous.invalid";
            member.DisplayName = "Removed Member";
            member.PasswordHash = "";
            member.RefreshToken = null;
            member.RefreshTokenExpiresAt = null;
            member.IsManaged = false;
            member.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Member removed" });
        })
        .WithDescription("Remove a family member (Parent only).");
    }
}

public record UpdateFamilyRequest(
    string? Name,
    string? CurrencyName,
    string? Timezone
);

public record ChangeRoleRequest(
    string Role
);
