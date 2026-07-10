using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Seeding;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Infrastructure.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Persistence;
using WorkspaceEcommerce.Infrastructure.Persistence.Queries;
using WorkspaceEcommerce.Infrastructure.Seeding;
using WorkspaceEcommerce.Infrastructure.Shipment;

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

        services.AddSingleton(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();

            return dataSourceBuilder.Build();
        });
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IAdminDashboardQuery, EfAdminDashboardQuery>();
        services.AddScoped<ICartStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICheckoutStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddSingleton(adminAuthOptions);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(configuration.GetSection(LoyaltyOptions.SectionName).Get<LoyaltyOptions>() ?? new LoyaltyOptions());
        services.AddSingleton<IAdminCredentialValidator, ConfiguredAdminCredentialValidator>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        var miniLogisticsOptions = configuration
            .GetSection(MiniLogisticsOptions.SectionName)
            .Get<MiniLogisticsOptions>() ?? new MiniLogisticsOptions();
        services.Configure<MiniLogisticsOptions>(configuration.GetSection(MiniLogisticsOptions.SectionName));
        services.AddHttpClient<IShipmentService, MiniLogisticsClient>(client =>
        {
            client.BaseAddress = new Uri(miniLogisticsOptions.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", miniLogisticsOptions.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
