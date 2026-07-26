namespace Village.Api.Tests;

public class MealTypeTests
{
    [Fact]
    public void MealType_Enum_HasAllExpectedValues()
    {
        var values = Enum.GetNames<Village.Domain.Entities.MealType>();
        Assert.Contains("Breakfast", values);
        Assert.Contains("Lunch", values);
        Assert.Contains("Dinner", values);
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void MealType_Enum_ValuesAreSequential()
    {
        var values = Enum.GetValues<Village.Domain.Entities.MealType>();
        var ordered = ((int[])Enum.GetValuesAsUnderlyingType<Village.Domain.Entities.MealType>()).Order();
        Assert.Equal(0, ordered.First());
        Assert.Equal(values.Length - 1, ordered.Last() - ordered.First());
    }
}
