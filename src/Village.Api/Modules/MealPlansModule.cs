using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class MealPlansModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meal-plans").RequireAuthorization();

        // GET /api/meal-plans — list meal plans for the family (optional week-start range)
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            DateOnly? week_start_from,
            DateOnly? week_start_to,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var query = db.MealPlans
                .Where(mp => mp.FamilyId == familyId.Value);

            if (week_start_from.HasValue)
                query = query.Where(mp => mp.WeekStart >= week_start_from.Value);
            if (week_start_to.HasValue)
                query = query.Where(mp => mp.WeekStart <= week_start_to.Value);

            var mealPlans = await query
                .OrderByDescending(mp => mp.WeekStart)
                .Select(mp => new
                {
                    mp.Id,
                    mp.WeekStart,
                    mp.WeekEnd,
                    mp.CreatedById,
                    mp.CreatedAt,
                    EntryCount = mp.Entries.Count
                })
                .ToListAsync(ct);

            return Results.Ok(mealPlans);
        })
        .WithDescription("Get meal plans for the family. Optional ?week_start_from= and ?week_start_to= filters.");

        // GET /api/meal-plans/{id} — get a single meal plan with entries
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .Include(mp => mp.Entries)
                    .ThenInclude(e => e.Recipe)
                .Include(mp => mp.CreatedBy)
                .Where(mp => mp.FamilyId == familyId.Value)
                .Select(mp => new
                {
                    mp.Id,
                    mp.WeekStart,
                    mp.WeekEnd,
                    CreatedById = mp.CreatedBy.Id,
                    CreatedByName = mp.CreatedBy.DisplayName,
                    mp.CreatedAt,
                    Entries = mp.Entries
                        .OrderBy(e => e.DayOfWeek)
                        .ThenBy(e => e.SortOrder)
                        .Select(e => new
                        {
                            e.Id,
                            e.DayOfWeek,
                            e.MealType,
                            RecipeId = e.Recipe != null ? e.Recipe.Id : (Guid?)null,
                            RecipeTitle = e.Recipe != null ? e.Recipe.Title : null,
                            e.Title,
                            e.SortOrder,
                            VoteCount = e.Votes.Count
                        })
                })
                .FirstOrDefaultAsync(ct);

            if (mealPlan == null) return Results.NotFound();

            return Results.Ok(mealPlan);
        })
        .WithDescription("Get a single meal plan with entries.");

        // POST /api/meal-plans — create a meal plan (auto-generates 7 days x 3 meals)
        group.MapPost("/", async (
            CreateMealPlanRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null || userId == null) return Results.Unauthorized();

            var weekStart = request.WeekStart;
            var weekEnd = weekStart.AddDays(6);

            // Check no overlapping plan
            var existing = await db.MealPlans
                .AnyAsync(mp => mp.FamilyId == familyId.Value && mp.WeekStart == weekStart, ct);
            if (existing)
                return Results.Conflict(new { error = "A meal plan already exists for this week." });

            var mealPlan = new MealPlan
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                CreatedById = userId.Value,
                CreatedAt = DateTime.UtcNow
            };

            // Auto-generate 21 entries: 7 days × 3 meal types (Breakfast, Lunch, Dinner)
            var mealTypes = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner };
            var entries = new List<MealPlanEntry>();
            for (int day = 0; day < 7; day++)
            {
                for (int mealIdx = 0; mealIdx < mealTypes.Length; mealIdx++)
                {
                    entries.Add(new MealPlanEntry
                    {
                        Id = Guid.NewGuid(),
                        MealPlanId = mealPlan.Id,
                        DayOfWeek = day,
                        MealType = mealTypes[mealIdx],
                        SortOrder = mealIdx
                    });
                }
            }

            db.MealPlans.Add(mealPlan);
            db.MealPlanEntries.AddRange(entries);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/meal-plans/{mealPlan.Id}", new
            {
                mealPlan.Id,
                mealPlan.WeekStart,
                mealPlan.WeekEnd,
                EntryCount = entries.Count
            });
        })
        .WithDescription("Create a new weekly meal plan with auto-generated slots.");

        // PUT /api/meal-plans/{id}/entries — add/update a meal plan entry
        group.MapPut("/{id:guid}/entries", async (
            Guid id,
            UpsertMealPlanEntryRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .FirstOrDefaultAsync(mp => mp.Id == id && mp.FamilyId == familyId.Value, ct);
            if (mealPlan == null) return Results.NotFound(new { error = "Meal plan not found" });

            // Find existing entry for this day/mealtype, or create new
            var existingEntry = await db.MealPlanEntries
                .FirstOrDefaultAsync(e =>
                    e.MealPlanId == id &&
                    e.DayOfWeek == request.DayOfWeek &&
                    e.MealType == request.MealType, ct);

            if (existingEntry != null)
            {
                // Update existing entry
                existingEntry.RecipeId = request.RecipeId;
                existingEntry.Title = request.Title?.Trim();
                if (request.SortOrder.HasValue)
                    existingEntry.SortOrder = request.SortOrder.Value;

                await db.SaveChangesAsync(ct);
                return Results.Ok(new
                {
                    existingEntry.Id,
                    existingEntry.DayOfWeek,
                    existingEntry.MealType,
                    existingEntry.RecipeId,
                    existingEntry.Title
                });
            }
            else
            {
                // Create new entry
                var newEntry = new MealPlanEntry
                {
                    Id = Guid.NewGuid(),
                    MealPlanId = id,
                    DayOfWeek = request.DayOfWeek,
                    MealType = request.MealType,
                    RecipeId = request.RecipeId,
                    Title = request.Title?.Trim(),
                    SortOrder = request.SortOrder ?? 0
                };

                db.MealPlanEntries.Add(newEntry);
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/meal-plans/{id}/entries/{newEntry.Id}", new
                {
                    newEntry.Id,
                    newEntry.DayOfWeek,
                    newEntry.MealType,
                    newEntry.RecipeId,
                    newEntry.Title
                });
            }
        })
        .WithDescription("Add or update a meal plan entry for a specific day/meal.");

        // DELETE /api/meal-plans/{id}/entries/{entryId} — remove an entry
        group.MapDelete("/{id:guid}/entries/{entryId:guid}", async (
            Guid id,
            Guid entryId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .FirstOrDefaultAsync(mp => mp.Id == id && mp.FamilyId == familyId.Value, ct);
            if (mealPlan == null) return Results.NotFound(new { error = "Meal plan not found" });

            var entry = await db.MealPlanEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == id, ct);
            if (entry == null) return Results.NotFound();

            db.MealPlanEntries.Remove(entry);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Remove a meal plan entry.");

        // POST /api/meal-plans/{id}/entries/{entryId}/vote — cast a vote
        group.MapPost("/{id:guid}/entries/{entryId:guid}/vote", async (
            Guid id,
            Guid entryId,
            CastVoteRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<MealPlanHub> mealPlanHub,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null || userId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .FirstOrDefaultAsync(mp => mp.Id == id && mp.FamilyId == familyId.Value, ct);
            if (mealPlan == null) return Results.NotFound(new { error = "Meal plan not found" });

            var entry = await db.MealPlanEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == id, ct);
            if (entry == null) return Results.NotFound(new { error = "Entry not found" });

            // Upsert vote: one vote per family member per entry
            var existingVote = await db.MealVotes
                .FirstOrDefaultAsync(v => v.MealPlanEntryId == entryId && v.FamilyMemberId == userId.Value, ct);

            if (existingVote != null)
            {
                existingVote.Preference = request.Preference;
                existingVote.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                db.MealVotes.Add(new MealVote
                {
                    Id = Guid.NewGuid(),
                    MealPlanEntryId = entryId,
                    FamilyMemberId = userId.Value,
                    Preference = request.Preference,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync(ct);

            // Real-time notification
            var displayName = httpContext.User.GetDisplayName() ?? "Someone";
            _ = mealPlanHub.NotifyMealPlanGroup(familyId.Value.ToString(), HubMethods.VoteUpdated, new
            {
                mealPlanId = id,
                entryId,
                familyMemberId = userId.Value,
                displayName,
                preference = request.Preference
            });

            return Results.Ok(new { entryId, preference = request.Preference });
        })
        .WithDescription("Cast or update a vote on a meal plan entry.");

        // GET /api/meal-plans/{id}/entries/{entryId}/votes — get vote tallies for an entry
        group.MapGet("/{id:guid}/entries/{entryId:guid}/votes", async (
            Guid id,
            Guid entryId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .FirstOrDefaultAsync(mp => mp.Id == id && mp.FamilyId == familyId.Value, ct);
            if (mealPlan == null) return Results.NotFound(new { error = "Meal plan not found" });

            var entry = await db.MealPlanEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == id, ct);
            if (entry == null) return Results.NotFound(new { error = "Entry not found" });

            var votes = await db.MealVotes
                .Include(v => v.FamilyMember)
                .Where(v => v.MealPlanEntryId == entryId)
                .Select(v => new
                {
                    v.Id,
                    v.Preference,
                    FamilyMemberId = v.FamilyMember.Id,
                    FamilyMemberName = v.FamilyMember.DisplayName,
                    v.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(votes);
        })
        .WithDescription("Get all votes for a meal plan entry.");
    }
}

// ── Request DTOs ──

public record CreateMealPlanRequest(
    DateOnly WeekStart
);

public record UpsertMealPlanEntryRequest(
    int DayOfWeek,
    MealType MealType,
    Guid? RecipeId,
    string? Title,
    int? SortOrder
);

public record CastVoteRequest(
    int Preference
);
