using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class MealsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var recipes = app.MapGroup("/api/recipes").RequireAuthorization();
        var mealPlans = app.MapGroup("/api/meal-plans").RequireAuthorization();

        // ── Recipes ──

        // GET /api/recipes — list recipes for the family
        recipes.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            bool? familyFavorites = httpContext.Request.Query["familyFavorites"].Count > 0
                ? bool.TryParse(httpContext.Request.Query["familyFavorites"], out var ff) && ff
                : null;
            var tag = httpContext.Request.Query["tag"].FirstOrDefault();

            var query = db.Recipes
                .Where(r => r.FamilyId == familyId.Value);

            if (familyFavorites == true)
                query = query.Where(r => r.IsFamilyFavorite);

            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(r => r.Tags != null && r.Tags.Contains(tag));

            var results = await query
                .OrderBy(r => r.Title)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Ingredients,
                    r.Instructions,
                    r.PrepTimeMinutes,
                    r.Servings,
                    Difficulty = r.Difficulty.ToString(),
                    r.Tags,
                    r.PhotoUrl,
                    r.IsFamilyFavorite,
                    r.CreatedById,
                    CreatedAt = r.CreatedAt.ToString("o")
                })
                .ToListAsync(ct);

            return Results.Ok(results);
        })
        .WithDescription("List recipes for the family, optionally filtered by favorites or tag.");

        // GET /api/recipes/{id} — get a single recipe
        recipes.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var recipe = await db.Recipes
                .Where(r => r.Id == id && r.FamilyId == familyId.Value)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Ingredients,
                    r.Instructions,
                    r.PrepTimeMinutes,
                    r.Servings,
                    Difficulty = r.Difficulty.ToString(),
                    r.Tags,
                    r.PhotoUrl,
                    r.IsFamilyFavorite,
                    r.CreatedById,
                    CreatedAt = r.CreatedAt.ToString("o")
                })
                .FirstOrDefaultAsync(ct);

            return recipe == null ? Results.NotFound() : Results.Ok(recipe);
        })
        .WithDescription("Get a single recipe by ID.");

        // POST /api/recipes — create a new recipe
        recipes.MapPost("/", async (
            CreateRecipeRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null || userId == null) return Results.Unauthorized();

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Ingredients = request.Ingredients.Trim(),
                Instructions = request.Instructions.Trim(),
                PrepTimeMinutes = request.PrepTimeMinutes,
                Servings = request.Servings,
                Difficulty = Enum.TryParse<RecipeDifficulty>(request.Difficulty, true, out var diff) ? diff : RecipeDifficulty.Easy,
                Tags = request.Tags?.Trim(),
                PhotoUrl = request.PhotoUrl?.Trim(),
                IsFamilyFavorite = request.IsFamilyFavorite,
                CreatedById = userId.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/recipes/{recipe.Id}", new
            {
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Ingredients,
                recipe.Instructions,
                recipe.PrepTimeMinutes,
                recipe.Servings,
                Difficulty = recipe.Difficulty.ToString(),
                recipe.Tags,
                recipe.PhotoUrl,
                recipe.IsFamilyFavorite,
                recipe.CreatedById,
                CreatedAt = recipe.CreatedAt.ToString("o")
            });
        })
        .WithDescription("Create a new recipe.");

        // PUT /api/recipes/{id} — update a recipe
        recipes.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRecipeRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var recipe = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == familyId.Value, ct);
            if (recipe == null) return Results.NotFound();

            if (request.Title != null) recipe.Title = request.Title.Trim();
            if (request.Description != null) recipe.Description = request.Description?.Trim();
            if (request.Ingredients != null) recipe.Ingredients = request.Ingredients.Trim();
            if (request.Instructions != null) recipe.Instructions = request.Instructions.Trim();
            if (request.PrepTimeMinutes.HasValue) recipe.PrepTimeMinutes = request.PrepTimeMinutes.Value;
            if (request.Servings.HasValue) recipe.Servings = request.Servings.Value;
            if (request.Difficulty != null)
                recipe.Difficulty = Enum.TryParse<RecipeDifficulty>(request.Difficulty, true, out var diff) ? diff : recipe.Difficulty;
            if (request.Tags != null) recipe.Tags = request.Tags?.Trim();
            if (request.PhotoUrl != null) recipe.PhotoUrl = request.PhotoUrl?.Trim();
            if (request.IsFamilyFavorite.HasValue) recipe.IsFamilyFavorite = request.IsFamilyFavorite.Value;
            recipe.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.Ingredients,
                recipe.Instructions,
                recipe.PrepTimeMinutes,
                recipe.Servings,
                Difficulty = recipe.Difficulty.ToString(),
                recipe.Tags,
                recipe.PhotoUrl,
                recipe.IsFamilyFavorite,
                recipe.CreatedById,
                CreatedAt = recipe.CreatedAt.ToString("o")
            });
        })
        .WithDescription("Update a recipe's properties.");

        // DELETE /api/recipes/{id} — delete a recipe
        recipes.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var recipe = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == familyId.Value, ct);
            if (recipe == null) return Results.NotFound();

            db.Recipes.Remove(recipe);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Delete a recipe.");

        // POST /api/recipes/{id}/toggle-favorite — toggle isFamilyFavorite
        recipes.MapPost("/{id:guid}/toggle-favorite", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var recipe = await db.Recipes
                .FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == familyId.Value, ct);
            if (recipe == null) return Results.NotFound();

            recipe.IsFamilyFavorite = !recipe.IsFamilyFavorite;
            recipe.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                recipe.Id,
                recipe.IsFamilyFavorite
            });
        })
        .WithDescription("Toggle the family favorite flag on a recipe.");

        // ── Meal Plans ──

        // GET /api/meal-plans — list meal plans (optional weekStart filter)
        mealPlans.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var weekStartStr = httpContext.Request.Query["weekStart"].FirstOrDefault();

            var query = db.MealPlans
                .Include(m => m.Entries)
                    .ThenInclude(e => e.Recipe)
                .Where(m => m.FamilyId == familyId.Value);

            if (DateOnly.TryParse(weekStartStr, out var weekStart))
                query = query.Where(m => m.WeekStart == weekStart);

            var results = await query
                .OrderByDescending(m => m.WeekStart)
                .Select(m => new
                {
                    m.Id,
                    WeekStart = m.WeekStart.ToString("yyyy-MM-dd"),
                    WeekEnd = m.WeekEnd.ToString("yyyy-MM-dd"),
                    m.CreatedById,
                    Entries = m.Entries.OrderBy(e => e.SortOrder).Select(e => new
                    {
                        e.Id,
                        e.MealPlanId,
                        e.DayOfWeek,
                        e.MealType,
                        RecipeId = e.RecipeId.ToString(),
                        e.Title,
                        RecipeTitle = e.Recipe != null ? e.Recipe.Title : null,
                        e.SortOrder
                    })
                })
                .ToListAsync(ct);

            return Results.Ok(results);
        })
        .WithDescription("Get meal plans for the family, optionally filtered by weekStart.");

        // GET /api/meal-plans/{id} — get a single meal plan with entries
        mealPlans.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .Include(m => m.Entries)
                    .ThenInclude(e => e.Recipe)
                .Where(m => m.Id == id && m.FamilyId == familyId.Value)
                .Select(m => new
                {
                    m.Id,
                    WeekStart = m.WeekStart.ToString("yyyy-MM-dd"),
                    WeekEnd = m.WeekEnd.ToString("yyyy-MM-dd"),
                    m.CreatedById,
                    Entries = m.Entries.OrderBy(e => e.SortOrder).Select(e => new
                    {
                        e.Id,
                        e.MealPlanId,
                        e.DayOfWeek,
                        e.MealType,
                        RecipeId = e.RecipeId.ToString(),
                        e.Title,
                        RecipeTitle = e.Recipe != null ? e.Recipe.Title : null,
                        e.SortOrder
                    })
                })
                .FirstOrDefaultAsync(ct);

            return mealPlan == null ? Results.NotFound() : Results.Ok(mealPlan);
        })
        .WithDescription("Get a single meal plan with its entries.");

        // POST /api/meal-plans — create a new meal plan for a week
        mealPlans.MapPost("/", async (
            CreateMealPlanRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null || userId == null) return Results.Unauthorized();

            if (!DateOnly.TryParse(request.WeekStart, out var weekStart))
                return Results.BadRequest(new { error = "Invalid weekStart date. Use yyyy-MM-dd format." });

            var weekEnd = weekStart.AddDays(6);

            // Check for existing plan overlapping this week
            var existing = await db.MealPlans
                .AnyAsync(m => m.FamilyId == familyId.Value && m.WeekStart == weekStart, ct);
            if (existing)
                return Results.Conflict(new { error = "A meal plan for this week already exists." });

            var mealPlan = new MealPlan
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                CreatedById = userId.Value,
                CreatedAt = DateTime.UtcNow
            };

            db.MealPlans.Add(mealPlan);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/meal-plans/{mealPlan.Id}", new
            {
                mealPlan.Id,
                WeekStart = mealPlan.WeekStart.ToString("yyyy-MM-dd"),
                WeekEnd = mealPlan.WeekEnd.ToString("yyyy-MM-dd"),
                mealPlan.CreatedById,
                Entries = new List<object>()
            });
        })
        .WithDescription("Create a new meal plan for a week.");

        // PUT /api/meal-plans/{mealPlanId}/entries — add an entry to a meal plan
        mealPlans.MapPut("/{mealPlanId:guid}/entries", async (
            Guid mealPlanId,
            AddEntryRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var mealPlan = await db.MealPlans
                .FirstOrDefaultAsync(m => m.Id == mealPlanId && m.FamilyId == familyId.Value, ct);
            if (mealPlan == null) return Results.NotFound(new { error = "Meal plan not found." });

            // If recipeId provided, verify it belongs to the same family
            if (request.RecipeId.HasValue)
            {
                var recipeExists = await db.Recipes
                    .AnyAsync(r => r.Id == request.RecipeId.Value && r.FamilyId == familyId.Value, ct);
                if (!recipeExists)
                    return Results.BadRequest(new { error = "Recipe not found in this family." });
            }

            var maxSortOrder = await db.MealPlanEntries
                .Where(e => e.MealPlanId == mealPlanId)
                .MaxAsync(e => (int?)e.SortOrder, ct) ?? 0;

            var entry = new MealPlanEntry
            {
                Id = Guid.NewGuid(),
                MealPlanId = mealPlanId,
                DayOfWeek = request.DayOfWeek,
                MealType = request.MealType,
                RecipeId = request.RecipeId,
                Title = request.Title?.Trim(),
                SortOrder = maxSortOrder + 1
            };

            db.MealPlanEntries.Add(entry);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/meal-plans/{mealPlanId}/entries", new
            {
                entry.Id,
                entry.MealPlanId,
                entry.DayOfWeek,
                entry.MealType,
                RecipeId = entry.RecipeId.ToString(),
                entry.Title,
                entry.SortOrder
            });
        })
        .WithDescription("Add an entry (meal slot) to a meal plan.");

        // DELETE /api/meal-plans/{mealPlanId}/entries/{entryId} — remove an entry
        mealPlans.MapDelete("/{mealPlanId:guid}/entries/{entryId:guid}", async (
            Guid mealPlanId,
            Guid entryId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var entry = await db.MealPlanEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == mealPlanId, ct);
            if (entry == null) return Results.NotFound();

            db.MealPlanEntries.Remove(entry);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Remove an entry from a meal plan.");

        // ── Voting ──

        // POST /api/meal-plans/{mealPlanId}/entries/{entryId}/vote — cast or update vote
        mealPlans.MapPost("/{mealPlanId:guid}/entries/{entryId:guid}/vote", async (
            Guid mealPlanId,
            Guid entryId,
            CastVoteRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null || userId == null) return Results.Unauthorized();

            if (request.Preference < 1 || request.Preference > 5)
                return Results.BadRequest(new { error = "Preference must be between 1 and 5." });

            var entry = await db.MealPlanEntries
                .Include(e => e.MealPlan)
                .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == mealPlanId, ct);
            if (entry == null) return Results.NotFound(new { error = "Entry not found." });
            if (entry.MealPlan.FamilyId != familyId.Value)
                return Results.Forbid();

            // Upsert vote
            var existingVote = await db.MealVotes
                .FirstOrDefaultAsync(v => v.MealPlanEntryId == entryId && v.FamilyMemberId == userId.Value, ct);

            if (existingVote != null)
            {
                existingVote.Preference = request.Preference;
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

            return Results.Ok(new { entryId, preference = request.Preference });
        })
        .WithDescription("Cast or update a vote on a meal plan entry.");

        // GET /api/meal-plans/{mealPlanId}/entries/{entryId}/votes — get votes tally
        mealPlans.MapGet("/{mealPlanId:guid}/entries/{entryId:guid}/votes", async (
            Guid mealPlanId,
            Guid entryId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var entry = await db.MealPlanEntries
                .Include(e => e.Recipe)
                .Include(e => e.Votes)
                    .ThenInclude(v => v.FamilyMember)
                .FirstOrDefaultAsync(e => e.Id == entryId && e.MealPlanId == mealPlanId, ct);
            if (entry == null) return Results.NotFound();

            var votes = entry.Votes.Select(v => new
            {
                MemberId = v.FamilyMemberId.ToString(),
                MemberName = v.FamilyMember.DisplayName,
                v.Preference
            });

            return Results.Ok(new
            {
                EntryId = entry.Id.ToString(),
                entry.MealType,
                entry.DayOfWeek,
                RecipeTitle = entry.Recipe?.Title,
                Votes = votes,
                TotalVotes = entry.Votes.Count
            });
        })
        .WithDescription("Get the vote tally for a meal plan entry.");
    }
}

// ── Request DTOs ──

public record CreateRecipeRequest(
    string Title,
    string? Description,
    string Ingredients,
    string Instructions,
    int PrepTimeMinutes = 30,
    int Servings = 4,
    string Difficulty = "Easy",
    string? Tags,
    string? PhotoUrl,
    bool IsFamilyFavorite = false
);

public record UpdateRecipeRequest(
    string? Title,
    string? Description,
    string? Ingredients,
    string? Instructions,
    int? PrepTimeMinutes,
    int? Servings,
    string? Difficulty,
    string? Tags,
    string? PhotoUrl,
    bool? IsFamilyFavorite
);

public record CreateMealPlanRequest(
    string WeekStart
);

public record AddEntryRequest(
    int DayOfWeek,
    string MealType,
    Guid? RecipeId,
    string? Title
);

public record CastVoteRequest(
    int Preference
);
