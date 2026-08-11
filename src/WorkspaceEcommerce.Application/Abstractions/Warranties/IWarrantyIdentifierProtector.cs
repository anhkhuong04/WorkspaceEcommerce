using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Abstractions.Warranties;

public interface IWarrantyIdentifierProtector
{
    WarrantyIdentifier Normalize(WarrantyIdentifierType? requestedType, string identifier);

    string CreateFingerprint(WarrantyIdentifierType identifierType, string normalizedIdentifier, int keyVersion);
}

public sealed record WarrantyIdentifier(
    WarrantyIdentifierType IdentifierType,
    string NormalizedValue,
    string MaskedValue);
