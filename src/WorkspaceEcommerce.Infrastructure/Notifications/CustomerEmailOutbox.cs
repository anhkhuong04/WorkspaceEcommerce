using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal sealed class CustomerEmailOutbox(
    IAppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider) : ICustomerEmailOutbox
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
        "WorkspaceEcommerce.CustomerEmailOutbox.v1");

    public void Enqueue(CustomerEmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = JsonSerializer.Serialize(message);
        dbContext.Add(new CustomerEmailOutboxMessage(
            Guid.NewGuid(),
            message.RecipientEmail.Trim().ToLowerInvariant(),
            message.Subject,
            _protector.Protect(payload),
            DateTimeOffset.UtcNow));
    }
}

internal sealed class CustomerEmailOutboxPayloadReader(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
        "WorkspaceEcommerce.CustomerEmailOutbox.v1");

    public CustomerEmailMessage Read(CustomerEmailOutboxMessage message)
    {
        var payload = _protector.Unprotect(message.ProtectedPayload);
        return JsonSerializer.Deserialize<CustomerEmailMessage>(payload)
            ?? throw new InvalidOperationException("Email outbox payload could not be read.");
    }
}
