using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FamilyBudget.API.Hubs;

[Authorize]
public class FamilyHub : Hub
{
    public async Task JoinFamilyGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"family_{familyId}");
    }

    public async Task LeaveFamilyGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"family_{familyId}");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Console.WriteLine($"User {userId} connected to SignalR");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Console.WriteLine($"User {userId} disconnected from SignalR");
        await base.OnDisconnectedAsync(exception);
    }
}