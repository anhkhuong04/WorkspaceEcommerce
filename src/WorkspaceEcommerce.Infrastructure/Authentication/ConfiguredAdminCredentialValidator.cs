using System.Security.Cryptography;
using System.Text;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Authentication;

internal sealed class ConfiguredAdminCredentialValidator(AdminAuthOptions options) : IAdminCredentialValidator
{
    public bool IsValid(string email, string password)
    {
        return string.Equals(
                options.Email.Trim(),
                email.Trim(),
                StringComparison.OrdinalIgnoreCase)
            && FixedTimeEquals(options.Password, password);
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
