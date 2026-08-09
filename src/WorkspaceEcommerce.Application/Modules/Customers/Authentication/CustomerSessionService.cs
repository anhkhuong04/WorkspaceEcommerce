using System.Security.Cryptography;
using System.Text;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Modules.Customers.Authentication;

internal sealed class CustomerSessionService(
    IAppDbContext dbContext,
    IJwtTokenGenerator tokenGenerator,
    CustomerAccountLifecycleOptions options,
    TimeProvider timeProvider) : ICustomerSessionService
{
    public async Task<CustomerAuthResponse> IssueAsync(
        Customer customer,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var family = new CustomerRefreshTokenFamily(
            Guid.NewGuid(),
            customer.Id,
            now,
            now.AddDays(options.RefreshTokenLifetimeDays));
        var refreshToken = GenerateToken();

        dbContext.Add(family);
        dbContext.Add(new CustomerRefreshToken(
            Guid.NewGuid(),
            family.Id,
            HashToken(refreshToken),
            now,
            family.ExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAuthResponse(customer, refreshToken);
    }

    public async Task<Result<CustomerAuthResponse>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<CustomerAuthResponse>.Unauthorized("Invalid or expired refresh token.");
        }

        var tokenHash = HashToken(refreshToken);
        CustomerAuthResponse? response = null;
        Result<CustomerAuthResponse>? failure = null;

        await dbContext.ExecuteInTransactionAsync(async transactionToken =>
        {
            var now = timeProvider.GetUtcNow();
            var token = await dbContext.FindCustomerRefreshTokenByHashForUpdateAsync(tokenHash, transactionToken);
            if (token is null)
            {
                failure = Result<CustomerAuthResponse>.Unauthorized("Invalid or expired refresh token.");
                return;
            }

            var family = dbContext.CustomerRefreshTokenFamilies
                .FirstOrDefault(candidate => candidate.Id == token.FamilyId);
            var customer = family is null
                ? null
                : dbContext.Customers.FirstOrDefault(candidate => candidate.Id == family.CustomerId);
            if (family is null || customer is null || !family.IsActiveAt(now))
            {
                failure = Result<CustomerAuthResponse>.Unauthorized("Invalid or expired refresh token.");
                return;
            }

            if (!token.IsUsableAt(now))
            {
                family.Revoke(now, token.UsedAt.HasValue ? "refresh_token_reuse" : "refresh_token_expired");
                await dbContext.SaveChangesAsync(transactionToken);
                failure = Result<CustomerAuthResponse>.Unauthorized("Invalid or expired refresh token.");
                return;
            }

            token.MarkUsed(now);
            var replacement = GenerateToken();
            dbContext.Add(new CustomerRefreshToken(
                Guid.NewGuid(),
                family.Id,
                HashToken(replacement),
                now,
                family.ExpiresAt));
            await dbContext.SaveChangesAsync(transactionToken);
            response = ToAuthResponse(customer, replacement);
        }, cancellationToken);

        return failure ?? Result<CustomerAuthResponse>.Success(response!);
    }

    public async Task RevokeAsync(
        string refreshToken,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var token = dbContext.CustomerRefreshTokens
            .FirstOrDefault(candidate => candidate.TokenHash == HashToken(refreshToken));
        if (token is null)
        {
            return;
        }

        var family = dbContext.CustomerRefreshTokenFamilies
            .FirstOrDefault(candidate => candidate.Id == token.FamilyId);
        if (family is null || family.RevokedAt.HasValue)
        {
            return;
        }

        family.Revoke(timeProvider.GetUtcNow(), reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(
        Guid customerId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var families = dbContext.CustomerRefreshTokenFamilies
            .Where(family => family.CustomerId == customerId && family.RevokedAt == null)
            .ToArray();
        foreach (var family in families)
        {
            family.Revoke(now, reason);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private CustomerAuthResponse ToAuthResponse(Customer customer, string refreshToken)
    {
        return tokenGenerator.GenerateCustomerToken(
            customer.Id,
            customer.Email,
            customer.FullName,
            customer.PhoneNumber) with { RefreshToken = refreshToken };
    }

    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
