namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public interface IAdminCredentialValidator
{
    bool IsValid(string email, string password);
}
