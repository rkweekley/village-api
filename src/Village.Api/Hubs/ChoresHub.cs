using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class ChoreHub : Hub
{
    public async Task JoinChoreGroup(string familyId)
    {
        var userFamilyId = Context.User?.FindFirst("family_id")?.Value;
        if (userFamilyId != familyId) throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chores:{familyId}");
    }

    public async Task LeaveChoreGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chores:{familyId}");
    }
}
