namespace WorkspaceEcommerce.Application.Abstractions.Notifications;

public interface ICustomerEmailOutbox
{
    void Enqueue(CustomerEmailMessage message);
}

public sealed record CustomerEmailMessage(
    string RecipientEmail,
    string Subject,
    string PlainTextBody);

public interface ICustomerEmailDeliveryService
{
    Task SendAsync(CustomerEmailMessage message, CancellationToken cancellationToken = default);
}
