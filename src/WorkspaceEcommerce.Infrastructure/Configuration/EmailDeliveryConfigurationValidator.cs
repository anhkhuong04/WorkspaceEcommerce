using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Configuration;

public static class EmailDeliveryConfigurationValidator
{
    public static EmailDeliveryOptions GetValidatedEmailDeliveryOptions(
        this IConfiguration configuration,
        string? environmentName)
    {
        var configured = configuration.GetSection(EmailDeliveryOptions.SectionName)
            .Get<EmailDeliveryOptions>() ?? new EmailDeliveryOptions();
        var provider = configured.Provider.Trim();
        var isLog = string.Equals(provider, "Log", StringComparison.OrdinalIgnoreCase);
        var isSmtp = string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase);

        if (!isLog && !isSmtp)
        {
            throw new InvalidOperationException("Configuration 'EmailDelivery:Provider' must be 'Log' or 'Smtp'.");
        }

        if (isLog && string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Configuration 'EmailDelivery:Provider' must be 'Smtp' outside Development.");
        }

        if (configured.WorkerIntervalSeconds is < 5 or > 3600)
        {
            throw new InvalidOperationException("Configuration 'EmailDelivery:WorkerIntervalSeconds' must be between 5 and 3600.");
        }

        if (isSmtp)
        {
            if (string.IsNullOrWhiteSpace(configured.Host) || configured.Port is < 1 or > 65535 ||
                string.IsNullOrWhiteSpace(configured.SenderEmail))
            {
                throw new InvalidOperationException("SMTP email delivery requires Host, Port, and SenderEmail configuration.");
            }

            try
            {
                _ = new MailAddress(configured.SenderEmail);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Configuration 'EmailDelivery:SenderEmail' must be a valid email address.", exception);
            }
        }

        return new EmailDeliveryOptions
        {
            Provider = isSmtp ? "Smtp" : "Log",
            SenderEmail = configured.SenderEmail.Trim(),
            Host = configured.Host?.Trim(),
            Port = configured.Port,
            EnableSsl = configured.EnableSsl,
            UserName = configured.UserName,
            Password = configured.Password,
            WorkerIntervalSeconds = configured.WorkerIntervalSeconds
        };
    }
}
