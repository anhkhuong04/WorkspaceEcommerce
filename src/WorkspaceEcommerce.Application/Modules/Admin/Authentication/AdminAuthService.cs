using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Admin.Authentication;

internal sealed class AdminAuthService(
    IValidator<AdminLoginRequest> validator,
    IAdminCredentialValidator credentialValidator,
    IJwtTokenGenerator tokenGenerator) : IAdminAuthService
{
    public async Task<Result<AdminLoginResponse>> LoginAsync(
        AdminLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminLoginResponse>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        if (!credentialValidator.IsValid(request.Email, request.Password))
        {
            return Result<AdminLoginResponse>.Unauthorized("Invalid email or password.");
        }

        var response = tokenGenerator.GenerateAdminToken(request.Email.Trim());

        return Result<AdminLoginResponse>.Success(response);
    }
}
