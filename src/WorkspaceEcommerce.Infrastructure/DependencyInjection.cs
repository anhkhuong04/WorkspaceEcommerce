using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Infrastructure.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetValidatedDefaultConnectionString();
        var adminAuthOptions = configuration.GetValidatedAdminAuthOptions();
        var jwtOptions = configuration.GetValidatedJwtOptions();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICartStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICheckoutStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton(adminAuthOptions);
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IAdminCredentialValidator, ConfiguredAdminCredentialValidator>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
