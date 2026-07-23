using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

public class FamilyHub : Hub
{
    public async Task JoinFamilyGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"family:{familyId}");
    }

    public async Task LeaveFamilyGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"family:{familyId}");
    }
}

public class ChoreHub : Hub
{
    public async Task JoinChoreGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chores:{familyId}");
    }
}

public class PointsHub : Hub
{
    public async Task JoinPointsGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"points:{familyId}");
    }
}
