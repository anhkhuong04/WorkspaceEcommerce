using System.Security.Cryptography;
using System.Text;
using WorkspaceEcommerce.Application.Abstractions.Warranties;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Warranties;

internal sealed class HmacWarrantyIdentifierProtector(WarrantyOptions options) : IWarrantyIdentifierProtector
{
    public WarrantyIdentifier Normalize(WarrantyIdentifierType? requestedType, string identifier)
    {
        var raw = identifier?.Trim() ?? string.Empty;
        if (raw.Length == 0 || raw.Length > 128)
        {
            throw new DomainException("Serial or IMEI is invalid.");
        }

        var compact = raw.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        var inferredType = compact.All(char.IsAsciiDigit) && compact.Length == 15
            ? WarrantyIdentifierType.Imei
            : WarrantyIdentifierType.Serial;
        var identifierType = requestedType ?? inferredType;

        if (!Enum.IsDefined(identifierType))
        {
            throw new DomainException("Identifier type is invalid.");
        }

        var normalized = identifierType == WarrantyIdentifierType.Imei
            ? NormalizeImei(compact)
            : NormalizeSerial(raw);

        return new WarrantyIdentifier(identifierType, normalized, Mask(normalized));
    }

    public string CreateFingerprint(WarrantyIdentifierType identifierType, string normalizedIdentifier, int keyVersion)
    {
        if (!Enum.IsDefined(identifierType) || string.IsNullOrWhiteSpace(normalizedIdentifier))
        {
            throw new ArgumentException("Warranty identifier fingerprint input is invalid.");
        }

        var key = options.IdentifierHmacKeys.TryGetValue(keyVersion, out var versionedKey)
            ? versionedKey
            : keyVersion == options.IdentifierKeyVersion ? options.IdentifierHmacKey : null;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Warranty identifier protection is not configured for this key version.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var material = $"v{keyVersion}|{identifierType}|{normalizedIdentifier}";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static string NormalizeImei(string compact)
    {
        if (compact.Length != 15 || !compact.All(char.IsAsciiDigit) || !IsValidLuhn(compact))
        {
            throw new DomainException("IMEI must contain 15 digits and pass its checksum.");
        }

        return compact;
    }

    private static string NormalizeSerial(string raw)
    {
        var normalized = raw.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 64 || !normalized.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            throw new DomainException("Serial must contain 3-64 letters, numbers, hyphens, underscores, or periods.");
        }

        return normalized;
    }

    private static string Mask(string normalized) => normalized.Length <= 4
        ? new string('*', normalized.Length)
        : new string('*', normalized.Length - 4) + normalized[^4..];

    private static bool IsValidLuhn(string value)
    {
        var sum = 0;
        for (var index = value.Length - 1; index >= 0; index--)
        {
            var digit = value[index] - '0';
            if ((value.Length - 1 - index) % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }
}
