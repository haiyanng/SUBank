using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SUBank.Application.Abstractions;

namespace SUBank.Api.Realtime;

[Authorize]
public sealed class BankingHub(IActiveSessionValidator sessionValidator) : Hub
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

        try
        {
            if (!await sessionValidator.IsValidAsync(userId, sessionId, Context.ConnectionAborted))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Session(sessionId));
            await base.OnConnectedAsync();
        }
        catch
        {
            Context.Abort();
            throw;
        }
    }
}

internal static class RealtimeGroups
{
    public static string Session(string sessionId) => $"session:{sessionId}";
}
