using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WorkspaceEcommerce.Application.Abstractions.Authentication;

namespace WorkspaceEcommerce.Api.Authentication;

internal sealed class CurrentCustomerContext(IHttpContextAccessor httpContextAccessor) : ICurrentCustomerContext
{
    public Guid? CustomerId
    {
        get
        {
            if (!IsAuthenticatedCustomer)
            {
                return null;
            }

            var customerId = GetClaimValue(ClaimTypes.NameIdentifier, JwtRegisteredClaimNames.Sub, "customer_id");
            return Guid.TryParse(customerId, out var parsedCustomerId) && parsedCustomerId != Guid.Empty
                ? parsedCustomerId
                : null;
        }
    }

    public string? Email => IsAuthenticatedCustomer
        ? GetClaimValue(ClaimTypes.Email, JwtRegisteredClaimNames.Email)
        : null;

    private bool IsAuthenticatedCustomer =>
        User?.Identity?.IsAuthenticated == true
        && User.IsInRole(AuthRoles.Customer);

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    private string? GetClaimValue(params string[] claimTypes)
    {
        return claimTypes
            .Select(claimType => User?.FindFirst(claimType)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
