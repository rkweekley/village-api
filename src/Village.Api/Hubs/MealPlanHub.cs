using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class MealPlanHub : Hub
{
    public async Task JoinMealPlanGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mealplan:{familyId}");
    }

    public async Task LeaveMealPlanGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"mealplan:{familyId}");
    }
}
