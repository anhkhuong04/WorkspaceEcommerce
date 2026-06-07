using WorkspaceEcommerce.Application.Modules.Admin.Authentication;

namespace WorkspaceEcommerce.Application.Abstractions.Authentication;

public interface IJwtTokenGenerator
{
    AdminLoginResponse GenerateAdminToken(string email);
}
