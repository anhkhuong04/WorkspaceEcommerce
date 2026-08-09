using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Notifications;

internal sealed class LoggingCustomerEmailDeliveryService(
    ILogger<LoggingCustomerEmailDeliveryService> logger) : ICustomerEmailDeliveryService
{
    public Task SendAsync(CustomerEmailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Do not log the body: account links contain raw single-use credentials.
        logger.LogInformation("Development email accepted for delivery. Recipient={Recipient}, Subject={Subject}",
            message.RecipientEmail, message.Subject);
        return Task.CompletedTask;
    }
}

internal sealed class SmtpCustomerEmailDeliveryService(
    EmailDeliveryOptions options) : ICustomerEmailDeliveryService
{
    public async Task SendAsync(CustomerEmailMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(options.Host!, options.Port)
        {
            EnableSsl = options.EnableSsl
        };
        if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        }

        using var mail = new MailMessage(options.SenderEmail, message.RecipientEmail, message.Subject, message.PlainTextBody)
        {
            IsBodyHtml = false
        };
        await client.SendMailAsync(mail, cancellationToken);
    }
}
