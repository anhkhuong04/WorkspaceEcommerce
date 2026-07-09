using Microsoft.AspNetCore.SignalR;
using WorkspaceEcommerce.Application.Abstractions.Notifications;

namespace WorkspaceEcommerce.Api.Hubs;

internal sealed class SignalRNotificationService(IHubContext<NotificationHub> hubContext) : INotificationService
{
    public async Task NotifyCustomerAsync(
        Guid customerId,
        string eventType,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"customer-{customerId}";
        await hubContext.Clients
            .Group(groupName)
            .SendAsync(eventType, payload, cancellationToken);
    }
}
