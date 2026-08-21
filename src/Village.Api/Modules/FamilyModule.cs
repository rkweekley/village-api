using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Api.Services;
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
                family.StripeCustomerId,
                family.StripeSubscriptionId,
                family.SubscriptionStatus,
                family.SubscriptionTier,
                family.SubscriptionExpiresAt,
                family.TrialEndsAt,
                Members = family.Members.Select(m => new
                {
                    m.Id,
                    m.DisplayName,
                    Email = m.IsManaged ? null : m.Email,
                    Role = m.Role.ToString(),
                    m.PointsBalance,
                    m.BirthDate,
                    m.IsManaged
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
            var role = httpContext.User.GetRole();
            if (role != "Parent") return Results.Forbid();

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
        .AddEndpointFilter<RequireSubscriptionFilter>()
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

            // Return only what registration needs (id + display name); don't leak
            // member count or re-echo the code to unauthenticated callers.
            return Results.Ok(new
            {
                family.Id,
                family.Name
            });
        })
        .AllowAnonymous()
        .WithDescription("Look up a family by invite code (used during registration).");

        // POST /api/families/mine/invite — send invite email
        group.MapPost("/mine/invite", async (
            HttpContext httpContext,
            VillageDbContext db,
            EmailBackgroundService emailQueue,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<SendInviteRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (familyId == null) return Results.Unauthorized();
            if (role != "Parent" && role != "Caregiver")
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                return Results.BadRequest(new { error = "A valid email address is required." });

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            // Queue invite email for reliable background delivery
            emailQueue.Enqueue(es => es.SendInviteEmailAsync(request.Email.Trim(), family.Name, family.InviteCode));

            return Results.Ok(new { message = $"Invite sent to {request.Email.Trim()}" });
        })
        .WithDescription("Send an invite email to join this family (Parent/Caregiver only).");

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

        // POST /api/families/mine/children — create a parent-managed child profile
        group.MapPost("/mine/children", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateChildRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (familyId == null) return Results.Unauthorized();
            if (role != "Parent" && role != "Caregiver")
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return Results.BadRequest(new { error = "A name is required." });

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            // The parent creating this profile is the parental-consent step for
            // collecting a minor's data (COPPA). The child cannot log in: no
            // password, and a synthetic non-deliverable email satisfies the
            // unique index on User.Email.
            var child = new User
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Email = $"managed_{Guid.NewGuid():N}@village.local",
                DisplayName = request.DisplayName.Trim(),
                Role = UserRole.Child,
                IsManaged = true,
                BirthDate = request.BirthDate,
                PasswordHash = string.Empty,
                PointsBalance = 0,
            };
            db.Users.Add(child);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/families/mine/children/{child.Id}", new
            {
                child.Id,
                child.DisplayName,
                Role = child.Role.ToString(),
                child.BirthDate,
                child.IsManaged,
                child.PointsBalance
            });
        })
        .WithDescription("Create a parent-managed child profile (Parent/Caregiver only).");

        // PATCH /api/families/mine/members/{userId} — update a managed child's name / birth date
        group.MapPatch("/mine/members/{userId:guid}", async (
            Guid userId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<UpdateMemberRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (familyId == null) return Results.Unauthorized();
            if (role != "Parent" && role != "Caregiver")
                return Results.Forbid();

            var member = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.FamilyId == familyId.Value, ct);
            if (member == null) return Results.NotFound();
            if (!member.IsManaged)
                return Results.BadRequest(new { error = "Only managed child profiles can be edited here." });

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
                member.DisplayName = request.DisplayName.Trim();
            if (request.BirthDate != null)
                member.BirthDate = request.BirthDate;
            member.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                member.Id,
                member.DisplayName,
                member.BirthDate,
                member.IsManaged
            });
        })
        .WithDescription("Update a managed child's name or birth date (Parent/Caregiver only).");
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

public record SendInviteRequest(
    string Email
);

public record CreateChildRequest(
    string DisplayName,
    DateOnly? BirthDate
);

public record UpdateMemberRequest(
    string? DisplayName,
    DateOnly? BirthDate
);
