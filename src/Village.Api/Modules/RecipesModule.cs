using System.Net;
using Carter;
using Village.Api.Services;

namespace Village.Api.Modules;

/// <summary>
/// Proxies TheMealDB — free recipe API for the "Ideas" section of meal planning.
/// </summary>
public class RecipesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes/ideas").RequireAuthorization();

        // GET /api/recipes/ideas/categories — list available categories for filter chips
        group.MapGet("/categories", async (MealDbService mealDb) =>
        {
            var response = await mealDb.GetCategoriesAsync();
            var categories = response?.Categories ?? [];
            return Results.Ok(categories.Select(c => new CategoryDto(
                c.IdCategory, c.StrCategory, c.StrCategoryThumb, c.StrCategoryDescription)));
        });

        // GET /api/recipes/ideas/category/{category} — browse recipes by category
        group.MapGet("/category/{category}", async (string category, MealDbService mealDb) =>
        {
            if (string.IsNullOrWhiteSpace(category))
                return Results.BadRequest(new { error = "Category is required" });
            var response = await mealDb.FilterByCategoryAsync(category);
            if (response?.Meals == null || response.Meals.Count == 0)
                return Results.Ok(Array.Empty<object>());
            // Filter endpoint returns summaries — return as-is (no ingredient detail yet)
            return Results.Ok(response.Meals.Select(m => new
            {
                id = m.IdMeal,
                title = m.StrMeal,
                image = m.StrMealThumb
            }));
        });

        // GET /api/recipes/ideas/search?q=chicken — search by name
        group.MapGet("/search", async (string q, MealDbService mealDb) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest(new { error = "Query is required" });
            var response = await mealDb.SearchByNameAsync(q);
            if (response?.Meals == null) return Results.Ok(Array.Empty<RecipeIdeaDto>());
            return Results.Ok(response.Meals.Select(ToRecipeIdea));
        });

        // GET /api/recipes/ideas/random — surprise me
        group.MapGet("/random", async (MealDbService mealDb) =>
        {
            var response = await mealDb.GetRandomAsync();
            var meal = response?.Meals?.FirstOrDefault();
            return meal != null ? Results.Ok(ToRecipeIdea(meal)) : Results.NotFound();
        });

        // GET /api/recipes/ideas/{id} — full recipe detail
        group.MapGet("/{id}", async (string id, MealDbService mealDb) =>
        {
            var response = await mealDb.LookupAsync(id);
            var meal = response?.Meals?.FirstOrDefault();
            return meal != null ? Results.Ok(ToRecipeIdea(meal)) : Results.NotFound();
        });
    }

    /// <summary>
    /// Clean TheMealDB instructions: decode HTML entities, normalize line endings,
    /// and add paragraph spacing between steps so they render as readable blocks.
    /// </summary>
    private static string CleanInstructions(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        // Decode HTML entities like &amp; &rsquo; &frac12;
        var decoded = WebUtility.HtmlDecode(raw);
        // Normalize \r\n → \n
        var normalized = decoded.Replace("\r\n", "\n").Replace("\r", "\n");
        // Collapse 3+ blank lines into 2
        while (normalized.Contains("\n\n\n"))
            normalized = normalized.Replace("\n\n\n", "\n\n");
        return normalized.Trim();
    }

    /// <summary>Convert TheMealDB's 20 numbered ingredient fields into a clean list.</summary>
    private static List<IngredientDto> ExtractIngredients(MealDbMealDetail meal)
    {
        var ingredients = new List<IngredientDto>();

        void AddIfPresent(string? name, string? measure)
        {
            if (!string.IsNullOrWhiteSpace(name))
                ingredients.Add(new IngredientDto(name.Trim(), measure?.Trim() ?? ""));
        }

        AddIfPresent(meal.StrIngredient1,  meal.StrMeasure1);
        AddIfPresent(meal.StrIngredient2,  meal.StrMeasure2);
        AddIfPresent(meal.StrIngredient3,  meal.StrMeasure3);
        AddIfPresent(meal.StrIngredient4,  meal.StrMeasure4);
        AddIfPresent(meal.StrIngredient5,  meal.StrMeasure5);
        AddIfPresent(meal.StrIngredient6,  meal.StrMeasure6);
        AddIfPresent(meal.StrIngredient7,  meal.StrMeasure7);
        AddIfPresent(meal.StrIngredient8,  meal.StrMeasure8);
        AddIfPresent(meal.StrIngredient9,  meal.StrMeasure9);
        AddIfPresent(meal.StrIngredient10, meal.StrMeasure10);
        AddIfPresent(meal.StrIngredient11, meal.StrMeasure11);
        AddIfPresent(meal.StrIngredient12, meal.StrMeasure12);
        AddIfPresent(meal.StrIngredient13, meal.StrMeasure13);
        AddIfPresent(meal.StrIngredient14, meal.StrMeasure14);
        AddIfPresent(meal.StrIngredient15, meal.StrMeasure15);
        AddIfPresent(meal.StrIngredient16, meal.StrMeasure16);
        AddIfPresent(meal.StrIngredient17, meal.StrMeasure17);
        AddIfPresent(meal.StrIngredient18, meal.StrMeasure18);
        AddIfPresent(meal.StrIngredient19, meal.StrMeasure19);
        AddIfPresent(meal.StrIngredient20, meal.StrMeasure20);

        return ingredients;
    }

    private static RecipeIdeaDto ToRecipeIdea(MealDbMealDetail meal) => new(
        Id: meal.IdMeal,
        Title: meal.StrMeal,
        Image: meal.StrMealThumb,
        Category: meal.StrCategory,
        Area: meal.StrArea,
        Instructions: CleanInstructions(meal.StrInstructions),
        YoutubeUrl: meal.StrYoutube,
        Ingredients: ExtractIngredients(meal));
}
