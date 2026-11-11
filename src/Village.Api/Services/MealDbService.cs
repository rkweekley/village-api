using Microsoft.Extensions.Caching.Memory;

namespace Village.Api.Services;

/// <summary>
/// Thin wrapper around TheMealDB's free recipe API.
/// No API key required — genuinely free, no quota, no billing.
/// Base URL: https://www.themealdb.com/api/json/v1/1/
/// </summary>
public class MealDbService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MealDbService> _logger;
    private const string BaseUrl = "https://www.themealdb.com/api/json/v1/1";

    // Near-static endpoints are cached so ordinary browsing doesn't re-fetch from the
    // free upstream API on every request. /random and /search stay uncached on purpose.
    private static readonly TimeSpan CategoriesTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FilterTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan LookupTtl = TimeSpan.FromHours(6);

    public MealDbService(HttpClient http, IMemoryCache cache, ILogger<MealDbService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Search recipes by name (uncached).</summary>
    public Task<MealDbSearchResponse?> SearchByNameAsync(string query) =>
        GetAsync<MealDbSearchResponse>($"{BaseUrl}/search.php?s={Uri.EscapeDataString(query)}");

    /// <summary>Browse recipes by category (Beef, Chicken, Seafood, etc.).</summary>
    public Task<MealDbFilterResponse?> FilterByCategoryAsync(string category) =>
        GetAsync<MealDbFilterResponse>(
            $"{BaseUrl}/filter.php?c={Uri.EscapeDataString(category)}",
            cacheKey: $"mealdb:filter:c:{category.ToLowerInvariant()}",
            ttl: FilterTtl);

    /// <summary>Filter recipes by main ingredient.</summary>
    public Task<MealDbFilterResponse?> FilterByIngredientAsync(string ingredient) =>
        GetAsync<MealDbFilterResponse>(
            $"{BaseUrl}/filter.php?i={Uri.EscapeDataString(ingredient)}",
            cacheKey: $"mealdb:filter:i:{ingredient.ToLowerInvariant()}",
            ttl: FilterTtl);

    /// <summary>Full recipe detail with up to 20 ingredient/measure pairs and instructions.</summary>
    public Task<MealDbLookupResponse?> LookupAsync(string mealId) =>
        GetAsync<MealDbLookupResponse>(
            $"{BaseUrl}/lookup.php?i={Uri.EscapeDataString(mealId)}",
            cacheKey: $"mealdb:lookup:{mealId}",
            ttl: LookupTtl);

    /// <summary>List all available recipe categories.</summary>
    public Task<MealDbCategoriesResponse?> GetCategoriesAsync() =>
        GetAsync<MealDbCategoriesResponse>(
            $"{BaseUrl}/categories.php",
            cacheKey: "mealdb:categories",
            ttl: CategoriesTtl);

    /// <summary>Get a random recipe ("Surprise Me") — uncached.</summary>
    public Task<MealDbLookupResponse?> GetRandomAsync() =>
        GetAsync<MealDbLookupResponse>($"{BaseUrl}/random.php");

    private async Task<T?> GetAsync<T>(string url, string? cacheKey = null, TimeSpan? ttl = null)
    {
        if (cacheKey != null && ttl.HasValue && _cache.TryGetValue<T>(cacheKey, out var cached) && cached is not null)
            return cached;

        var result = await GetWithRetryAsync<T>(url);

        if (cacheKey != null && ttl.HasValue && result is not null)
            _cache.Set(cacheKey, result, ttl.Value);

        return result;
    }

    /// <summary>Fetch with a single retry on transient upstream failures.</summary>
    private async Task<T?> GetWithRetryAsync<T>(string url)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _http.GetFromJsonAsync<T>(url);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "MealDB request failed (attempt {Attempt}/{Max}): {Url} (status {Status})",
                    attempt, maxAttempts, url, ex.StatusCode);
                if (attempt >= maxAttempts) throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "MealDB request timed out (attempt {Attempt}/{Max}): {Url}",
                    attempt, maxAttempts, url);
                if (attempt >= maxAttempts) throw;
            }
        }

        return default; // unreachable — the final attempt rethrows above
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
