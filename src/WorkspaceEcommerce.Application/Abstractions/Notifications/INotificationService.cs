namespace WorkspaceEcommerce.Application.Abstractions.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Sends a real-time notification to a specific customer.
    /// </summary>
    Task NotifyCustomerAsync(Guid customerId, string eventType, object payload, CancellationToken cancellationToken = default);
}
