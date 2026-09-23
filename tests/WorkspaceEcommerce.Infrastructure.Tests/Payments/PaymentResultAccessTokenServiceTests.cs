using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using WorkspaceEcommerce.Infrastructure.Payments;

namespace WorkspaceEcommerce.Infrastructure.Tests.Payments;

public sealed class PaymentResultAccessTokenServiceTests
{
    [Fact]
    public void IssueAndValidate_MatchingProof_ReturnsScopedGrantUntilExpiry()
    {
        var now = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(now);
        var service = new DataProtectionPaymentResultAccessTokenService(
            new AuthenticatedTestDataProtectionProvider(),
            timeProvider);
        var orderId = Guid.NewGuid();

        var token = service.Issue(orderId, "ord-pay-0001");
        var isValid = service.TryValidate(token, out var grant);

        Assert.True(isValid);
        Assert.Equal(orderId, grant.OrderId);
        Assert.Equal("ORD-PAY-0001", grant.OrderCode);

        timeProvider.Advance(TimeSpan.FromMinutes(15));
        Assert.False(service.TryValidate(token, out _));
    }

    [Fact]
    public void TryValidate_TamperedOrMalformedProof_ReturnsFalse()
    {
        var service = new DataProtectionPaymentResultAccessTokenService(
            new AuthenticatedTestDataProtectionProvider(),
            new TestTimeProvider(DateTimeOffset.UtcNow));
        var token = service.Issue(Guid.NewGuid(), "ORD-PAY-0002");

        Assert.False(service.TryValidate(token + "tampered", out _));
        Assert.False(service.TryValidate("not-a-protected-token", out _));
        Assert.False(service.TryValidate(null, out _));
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    private sealed class AuthenticatedTestDataProtectionProvider : IDataProtectionProvider
    {
        private static readonly byte[] Key = SHA256.HashData("payment-result-test-key"u8.ToArray());

        public IDataProtector CreateProtector(string purpose)
        {
            return new AuthenticatedTestDataProtector(purpose, Key);
        }
    }

    private sealed class AuthenticatedTestDataProtector(string purpose, byte[] key) : IDataProtector
    {
        public IDataProtector CreateProtector(string childPurpose)
        {
            return new AuthenticatedTestDataProtector($"{purpose}.{childPurpose}", key);
        }

        public byte[] Protect(byte[] plaintext)
        {
            var purposeBytes = System.Text.Encoding.UTF8.GetBytes(purpose);
            var authenticatedPayload = purposeBytes.Concat(plaintext).ToArray();
            var signature = HMACSHA256.HashData(key, authenticatedPayload);
            return signature.Concat(plaintext).ToArray();
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            if (protectedData.Length <= 32)
            {
                throw new CryptographicException("Protected payload is invalid.");
            }

            var signature = protectedData[..32];
            var plaintext = protectedData[32..];
            var purposeBytes = System.Text.Encoding.UTF8.GetBytes(purpose);
            var expectedSignature = HMACSHA256.HashData(key, purposeBytes.Concat(plaintext).ToArray());
            if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
            {
                throw new CryptographicException("Protected payload authentication failed.");
            }

            return plaintext;
        }
    }
}
