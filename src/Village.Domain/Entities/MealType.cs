namespace Village.Domain.Entities;

/// <summary>
/// Represents the type of meal in a meal plan entry.
/// Stored as a string in the database for readability.
/// </summary>
public enum MealType
{
    Breakfast = 0,
    Lunch = 1,
    Dinner = 2
}
