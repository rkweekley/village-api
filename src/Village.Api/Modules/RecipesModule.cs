using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class RecipesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes").RequireAuthorization();

        // GET /api/recipes — list recipes for the family
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            bool? family_favorites,
            string? tag,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var query = db.Recipes
                .Include(r => r.CreatedBy)
                .Where(r => r.FamilyId == familyId.Value && r.IsActive);

            if (family_favorites == true)
            {
                query = query.Where(r => r.IsFamilyFavorite);
            }

            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(r => r.Tags.Contains(tag));
            }

            var recipes = await query
                .OrderBy(r => r.Title)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.PrepTimeMinutes,
                    r.Servings,
                    r.Difficulty,
                    r.Tags,
                    r.PhotoUrl,
                    r.IsFamilyFavorite,
                    CreatedById = r.CreatedBy.Id,
                    CreatedByName = r.CreatedBy.DisplayName,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(recipes);
        })
        .WithDescription("Get recipes for the family. Optional ?family_favorites=true and ?tag=filter.");

        // GET /api/recipes/{id} — get a single recipe
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var recipe = await db.Recipes
                .Include(r => r.CreatedBy)
                .Where(r => r.FamilyId == familyId.Value && r.IsActive)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Ingredients,
                    r.Instructions,
                    r.PrepTimeMinutes,
                    r.Servings,
                    r.Difficulty,
                    r.Tags,
                    r.PhotoUrl,
                    r.IsFamilyFavorite,
                    CreatedById = r.CreatedBy.Id,
                    CreatedByName = r.CreatedBy.DisplayName,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .FirstOrDefaultAsync(ct);

            if (recipe == null) return Results.NotFound();

            return Results.Ok(recipe);
        })
        .WithDescription("Get a single recipe by ID.");

        // POST /api/recipes — create a recipe
        group.MapPost("/", async (
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
                Difficulty = request.Difficulty,
                Tags = request.Tags ?? string.Empty,
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
                recipe.PrepTimeMinutes,
                recipe.Servings,
                recipe.Difficulty
            });
        })
        .WithDescription("Create a new recipe.");

        // PUT /api/recipes/{id} — update a recipe
        group.MapPut("/{id:guid}", async (
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
            if (request.Difficulty != null) recipe.Difficulty = request.Difficulty;
            if (request.Tags != null) recipe.Tags = request.Tags;
            if (request.PhotoUrl != null) recipe.PhotoUrl = request.PhotoUrl?.Trim();
            if (request.IsFamilyFavorite.HasValue) recipe.IsFamilyFavorite = request.IsFamilyFavorite.Value;
            if (request.IsActive.HasValue) recipe.IsActive = request.IsActive.Value;
            recipe.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { recipe.Id, recipe.Title });
        })
        .WithDescription("Update a recipe's properties.");

        // DELETE /api/recipes/{id} — soft-delete a recipe
        group.MapDelete("/{id:guid}", async (
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

            recipe.IsActive = false;
            recipe.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Soft-delete a recipe (marks inactive).");
    }
}

// ── Request DTOs ──

public record CreateRecipeRequest(
    string Title,
    string? Description,
    string Ingredients,
    string Instructions,
    int PrepTimeMinutes,
    string? Tags,
    string? PhotoUrl,
    int Servings = 4,
    string Difficulty = "Easy",
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
    bool? IsFamilyFavorite,
    bool? IsActive
);
