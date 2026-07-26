using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class SchoolHub : Hub
{
    public async Task JoinSchoolGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"school:{familyId}");
    }

    public async Task LeaveSchoolGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"school:{familyId}");
    }
}
