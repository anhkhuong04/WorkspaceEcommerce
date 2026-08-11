using WorkspaceEcommerce.Application.Abstractions.Warranties;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Warranties;
using WorkspaceEcommerce.Infrastructure.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Tests.Warranties;

public sealed class HmacWarrantyIdentifierProtectorTests
{
    private static readonly WarrantyOptions Options = new()
    {
        Enabled = true,
        IdentifierKeyVersion = 1,
        IdentifierHmacKey = "test-warranty-hmac-key-at-least-32-characters"
    };

    [Fact]
    public void Normalize_Serial_StoresOnlyNormalizedMaskedAndFingerprintableValues()
    {
        var protector = new HmacWarrantyIdentifierProtector(Options);

        var identifier = protector.Normalize(WarrantyIdentifierType.Serial, " serial-001 ");
        var fingerprint = protector.CreateFingerprint(identifier.IdentifierType, identifier.NormalizedValue, 1);

        Assert.Equal("SERIAL-001", identifier.NormalizedValue);
        Assert.Equal("******-001", identifier.MaskedValue);
        Assert.DoesNotContain("SERIAL-001", fingerprint, StringComparison.Ordinal);
        Assert.Equal(64, fingerprint.Length);
    }

    [Fact]
    public void Normalize_ValidImei_InfersImeiAndValidatesLuhn()
    {
        var protector = new HmacWarrantyIdentifierProtector(Options);

        var identifier = protector.Normalize(null, "490154203237518");

        Assert.Equal(WarrantyIdentifierType.Imei, identifier.IdentifierType);
        Assert.Equal("***********7518", identifier.MaskedValue);
    }

    [Fact]
    public void Normalize_InvalidImei_RejectsInput()
    {
        var protector = new HmacWarrantyIdentifierProtector(Options);

        Assert.Throws<DomainException>(() => protector.Normalize(WarrantyIdentifierType.Imei, "490154203237519"));
    }

    [Fact]
    public void CreateFingerprint_UsesConfiguredPreviousKeyDuringRotation()
    {
        var protector = new HmacWarrantyIdentifierProtector(new WarrantyOptions
        {
            Enabled = true,
            IdentifierKeyVersion = 2,
            IdentifierHmacKey = "current-warranty-hmac-key-at-least-32-characters",
            IdentifierHmacKeys = new Dictionary<int, string>
            {
                [1] = "previous-warranty-hmac-key-at-least-32-characters"
            }
        });

        var oldFingerprint = protector.CreateFingerprint(WarrantyIdentifierType.Serial, "SERIAL-001", 1);
        var currentFingerprint = protector.CreateFingerprint(WarrantyIdentifierType.Serial, "SERIAL-001", 2);

        Assert.NotEqual(oldFingerprint, currentFingerprint);
        Assert.Equal(64, oldFingerprint.Length);
        Assert.Equal(64, currentFingerprint.Length);
    }
}
