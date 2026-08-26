using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SUBank.Api.Realtime;

[Authorize]
public sealed class BankingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = Context.User?.FindFirstValue("sid");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId));
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Session(sessionId));
        await base.OnConnectedAsync();
    }
}

internal static class RealtimeGroups
{
    public static string User(string userId) => $"user:{userId}";
    public static string Session(string sessionId) => $"session:{sessionId}";
}
