using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class FamilyHub : Hub
{
    public async Task JoinFamilyGroup(string familyId)
    {
        var userFamilyId = Context.User?.FindFirst("family_id")?.Value;
        if (userFamilyId != familyId) throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"family:{familyId}");
    }

    public async Task LeaveFamilyGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"family:{familyId}");
    }
}
