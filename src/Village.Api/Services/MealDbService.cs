namespace Village.Api.Services;

/// <summary>
/// Thin wrapper around TheMealDB's free recipe API.
/// No API key required — genuinely free, no quota, no billing.
/// Base URL: https://www.themealdb.com/api/json/v1/1/
/// </summary>
public class MealDbService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://www.themealdb.com/api/json/v1/1";

    public MealDbService(HttpClient http) => _http = http;

    /// <summary>Search recipes by name.</summary>
    public async Task<MealDbSearchResponse?> SearchByNameAsync(string query)
    {
        var url = $"{BaseUrl}/search.php?s={Uri.EscapeDataString(query)}";
        return await _http.GetFromJsonAsync<MealDbSearchResponse>(url);
    }

    /// <summary>Browse recipes by category (Beef, Chicken, Seafood, etc.).</summary>
    public async Task<MealDbFilterResponse?> FilterByCategoryAsync(string category)
    {
        var url = $"{BaseUrl}/filter.php?c={Uri.EscapeDataString(category)}";
        return await _http.GetFromJsonAsync<MealDbFilterResponse>(url);
    }

    /// <summary>Filter recipes by main ingredient.</summary>
    public async Task<MealDbFilterResponse?> FilterByIngredientAsync(string ingredient)
    {
        var url = $"{BaseUrl}/filter.php?i={Uri.EscapeDataString(ingredient)}";
        return await _http.GetFromJsonAsync<MealDbFilterResponse>(url);
    }

    /// <summary>Full recipe detail with up to 20 ingredient/measure pairs and instructions.</summary>
    public async Task<MealDbLookupResponse?> LookupAsync(string mealId)
    {
        var url = $"{BaseUrl}/lookup.php?i={Uri.EscapeDataString(mealId)}";
        return await _http.GetFromJsonAsync<MealDbLookupResponse>(url);
    }

    /// <summary>List all available recipe categories.</summary>
    public async Task<MealDbCategoriesResponse?> GetCategoriesAsync()
    {
        var url = $"{BaseUrl}/categories.php";
        return await _http.GetFromJsonAsync<MealDbCategoriesResponse>(url);
    }

    /// <summary>Get a random recipe ("Surprise Me").</summary>
    public async Task<MealDbLookupResponse?> GetRandomAsync()
    {
        var url = $"{BaseUrl}/random.php";
        return await _http.GetFromJsonAsync<MealDbLookupResponse>(url);
    }
}

// ── TheMealDB response DTOs ──

/// <summary>Search result — includes full ingredient + instruction detail.</summary>
public sealed record MealDbSearchResponse(List<MealDbMealDetail>? Meals);

/// <summary>Category / ingredient filter result — summary only.</summary>
public sealed record MealDbFilterResponse(List<MealDbMealSummary>? Meals);

/// <summary>Lookup result — full detail for a single recipe.</summary>
public sealed record MealDbLookupResponse(List<MealDbMealDetail>? Meals);

/// <summary>Categories list.</summary>
public sealed record MealDbCategoriesResponse(List<MealDbCategory> Categories);

public sealed record MealDbMealSummary(string IdMeal, string StrMeal, string StrMealThumb);

public sealed record MealDbMealDetail(
    string IdMeal,
    string StrMeal,
    string? StrCategory,
    string? StrArea,
    string StrInstructions,
    string StrMealThumb,
    string? StrYoutube,
    string? StrIngredient1,  string? StrMeasure1,
    string? StrIngredient2,  string? StrMeasure2,
    string? StrIngredient3,  string? StrMeasure3,
    string? StrIngredient4,  string? StrMeasure4,
    string? StrIngredient5,  string? StrMeasure5,
    string? StrIngredient6,  string? StrMeasure6,
    string? StrIngredient7,  string? StrMeasure7,
    string? StrIngredient8,  string? StrMeasure8,
    string? StrIngredient9,  string? StrMeasure9,
    string? StrIngredient10, string? StrMeasure10,
    string? StrIngredient11, string? StrMeasure11,
    string? StrIngredient12, string? StrMeasure12,
    string? StrIngredient13, string? StrMeasure13,
    string? StrIngredient14, string? StrMeasure14,
    string? StrIngredient15, string? StrMeasure15,
    string? StrIngredient16, string? StrMeasure16,
    string? StrIngredient17, string? StrMeasure17,
    string? StrIngredient18, string? StrMeasure18,
    string? StrIngredient19, string? StrMeasure19,
    string? StrIngredient20, string? StrMeasure20);

public sealed record MealDbCategory(
    string IdCategory,
    string StrCategory,
    string StrCategoryThumb,
    string StrCategoryDescription);

// ── Clean DTOs returned to the frontend ──

/// <summary>Recipe returned to Flutter — ingredient pairs collapsed into a clean list.</summary>
public sealed record RecipeIdeaDto(
    string Id,
    string Title,
    string Image,
    string? Category,
    string? Area,
    string Instructions,
    string? YoutubeUrl,
    List<IngredientDto> Ingredients);

public sealed record IngredientDto(string Name, string Measure);

public sealed record CategoryDto(string Id, string Name, string Thumb, string Description);
