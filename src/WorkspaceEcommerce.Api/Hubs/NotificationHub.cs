using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

namespace WorkspaceEcommerce.Api.Hubs;

[Authorize(Roles = AuthRoles.Customer)]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"customer-{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"customer-{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
