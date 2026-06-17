using WorkspaceEcommerce.Infrastructure.Authentication;

namespace WorkspaceEcommerce.Infrastructure.Tests.Authentication;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsHashThatCanBeVerified()
    {
        var hasher = new PasswordHasher();

        var passwordHash = hasher.Hash("customer-password");

        Assert.True(hasher.Verify("customer-password", passwordHash));
    }

    [Fact]
    public void Verify_WhenPasswordDoesNotMatch_ReturnsFalse()
    {
        var hasher = new PasswordHasher();
        var passwordHash = hasher.Hash("customer-password");

        Assert.False(hasher.Verify("wrong-password", passwordHash));
    }

    [Fact]
    public void Hash_SamePassword_UsesDifferentSalt()
    {
        var hasher = new PasswordHasher();

        var firstHash = hasher.Hash("customer-password");
        var secondHash = hasher.Hash("customer-password");

        Assert.NotEqual(firstHash, secondHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("PBKDF2-SHA256:not-number:salt:hash")]
    public void Verify_WhenHashFormatIsInvalid_ReturnsFalse(string passwordHash)
    {
        var hasher = new PasswordHasher();

        Assert.False(hasher.Verify("customer-password", passwordHash));
    }
}
