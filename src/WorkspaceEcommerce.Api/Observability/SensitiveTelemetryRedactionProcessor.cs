using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace WorkspaceEcommerce.Api.Observability;

/// <summary>
/// Removes credentials and request-specific secrets from telemetry immediately before it is sent.
/// This is deliberately a processor (rather than logging conventions alone) so custom telemetry
/// emitted by application code is subject to the same boundary.
/// </summary>
public sealed class SensitiveTelemetryRedactionProcessor(ITelemetryProcessor next) : ITelemetryProcessor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly Regex QueryValuePattern = new(
        @"(?<prefix>[?&][^?&#=\s]+)=([^&#\s]*)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex SecretAssignmentPattern = new(
        @"(?<prefix>\b(?:authorization|proxy-authorization|cookie|set-cookie|x-api-key|api[_-]?key|access[_-]?token|refresh[_-]?token|id[_-]?token|client[_-]?secret|password|passwd|pwd|secret|credential|connection[_-]?string|private[_-]?key|signing[_-]?key|encryption[_-]?key|otp|totp|two[_-]?factor(?:[_-]?code)?|recovery[_-]?code|verification[_-]?code|webhook[_-]?signature|signature|request[_-]?body|response[_-]?body|payload|session(?:[_-]?id)?|csrf(?:[_-]?token)?|nonce|serial|imei|identifier)\b\s*(?:=|:)\s*)(?:""[^""]*""|'[^']*'|[^\s,;&]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex BearerTokenPattern = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex EmailAddressPattern = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly ITelemetryProcessor _next = next ?? throw new ArgumentNullException(nameof(next));

    public void Process(ITelemetry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Redact(item);
        _next.Process(item);
    }

    private static void Redact(ITelemetry item)
    {
        if (item is ISupportProperties supportProperties)
        {
            foreach (var propertyName in supportProperties.Properties.Keys.ToArray())
            {
                var propertyValue = supportProperties.Properties[propertyName];
                supportProperties.Properties[propertyName] = IsSensitivePropertyName(propertyName)
                    ? RedactedValue
                    : SanitizeText(propertyValue);
            }
        }

        switch (item)
        {
            case RequestTelemetry request:
                if (request.Url is not null)
                {
                    request.Url = SanitizeUri(request.Url);
                }

                request.Name = SanitizeText(request.Name);
                break;

            case DependencyTelemetry dependency:
                dependency.Name = SanitizeText(dependency.Name);
                dependency.Data = SanitizeText(dependency.Data);
                dependency.Target = SanitizeText(dependency.Target);
                break;

            case TraceTelemetry trace:
                trace.Message = SanitizeText(trace.Message);
                break;

            case ExceptionTelemetry exception:
                exception.Message = SanitizeText(exception.Message);
                foreach (var detail in exception.ExceptionDetailsInfoList)
                {
                    detail.Message = SanitizeText(detail.Message);
                }

                break;

            case EventTelemetry @event:
                @event.Name = SanitizeText(@event.Name);
                break;
        }
    }

    private static bool IsSensitivePropertyName(string propertyName)
    {
        var normalized = string.Concat(propertyName.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        return normalized.Contains("authorization", StringComparison.Ordinal) ||
               normalized.Contains("cookie", StringComparison.Ordinal) ||
               normalized.Contains("password", StringComparison.Ordinal) ||
               normalized.Contains("email", StringComparison.Ordinal) ||
               normalized.Contains("passwd", StringComparison.Ordinal) ||
               normalized == "pwd" ||
               normalized.Contains("secret", StringComparison.Ordinal) ||
               normalized.Contains("token", StringComparison.Ordinal) ||
               normalized.Contains("apikey", StringComparison.Ordinal) ||
               normalized.Contains("credential", StringComparison.Ordinal) ||
               normalized.Contains("connectionstring", StringComparison.Ordinal) ||
               normalized.Contains("privatekey", StringComparison.Ordinal) ||
               normalized.Contains("signingkey", StringComparison.Ordinal) ||
               normalized.Contains("encryptionkey", StringComparison.Ordinal) ||
               normalized.Contains("recoverycode", StringComparison.Ordinal) ||
               normalized.Contains("verificationcode", StringComparison.Ordinal) ||
               normalized.Contains("twofactor", StringComparison.Ordinal) ||
               normalized.Contains("totp", StringComparison.Ordinal) ||
               normalized == "otp" ||
               normalized.Contains("session", StringComparison.Ordinal) ||
               normalized.Contains("csrf", StringComparison.Ordinal) ||
               normalized == "nonce" ||
               normalized.Contains("requestbody", StringComparison.Ordinal) ||
               normalized.Contains("responsebody", StringComparison.Ordinal) ||
               normalized.Contains("webhookbody", StringComparison.Ordinal) ||
               normalized == "payload" ||
               normalized.Contains("webhooksignature", StringComparison.Ordinal) ||
               normalized.Contains("serial", StringComparison.Ordinal) ||
               normalized.Contains("imei", StringComparison.Ordinal) ||
               normalized.Contains("identifier", StringComparison.Ordinal);
    }

    private static Uri SanitizeUri(Uri value)
    {
        if (!value.IsAbsoluteUri)
        {
            return value;
        }

        var sanitized = new UriBuilder(value)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return sanitized.Uri;
    }

    private static string SanitizeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        try
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            {
                return SanitizeUri(absoluteUri).ToString();
            }

            var withoutBearerTokens = BearerTokenPattern.Replace(value, $"Bearer {RedactedValue}");
            var withoutQueryValues = QueryValuePattern.Replace(
                withoutBearerTokens,
                match => $"{match.Groups["prefix"].Value}={RedactedValue}");
            var withoutSecretAssignments = SecretAssignmentPattern.Replace(
                withoutQueryValues,
                match => $"{match.Groups["prefix"].Value}{RedactedValue}");
            return EmailAddressPattern.Replace(withoutSecretAssignments, RedactedValue);
        }
        catch (RegexMatchTimeoutException)
        {
            // Input may be caller-controlled. Failing closed avoids passing an uninspected value to telemetry.
            return RedactedValue;
        }
    }
}
