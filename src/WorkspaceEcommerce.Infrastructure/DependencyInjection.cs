using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Seeding;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Infrastructure.Authentication;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Notifications;
using WorkspaceEcommerce.Infrastructure.Media;
using WorkspaceEcommerce.Infrastructure.Payments;
using WorkspaceEcommerce.Infrastructure.Persistence;
using WorkspaceEcommerce.Infrastructure.Persistence.Queries;
using WorkspaceEcommerce.Infrastructure.Seeding;
using WorkspaceEcommerce.Infrastructure.Shipment;

namespace WorkspaceEcommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetValidatedDefaultConnectionString();
        var adminAuthOptions = configuration.GetValidatedAdminAuthOptions();
        var jwtOptions = configuration.GetValidatedJwtOptions();
        var googleAuthOptions = configuration.GetValidatedGoogleAuthOptions();
        var twoFactorOptions = configuration.GetValidatedTwoFactorOptions();
        var customerAccountLifecycleOptions = configuration.GetValidatedCustomerAccountLifecycleOptions();
        var emailDeliveryOptions = configuration.GetValidatedEmailDeliveryOptions(environment.EnvironmentName);
        var mediaStorageOptions = configuration.GetValidatedMediaStorageOptions(environment.EnvironmentName);

        services.AddSingleton(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();

            return dataSourceBuilder.Build();
        });
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>());
            var interceptors = provider.GetServices<IInterceptor>().ToArray();
            if (interceptors.Length > 0)
            {
                options.AddInterceptors(interceptors);
            }
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICatalogReadStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderReadStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ILoyaltyReadStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IAppWriteStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IAdminDashboardQuery, EfAdminDashboardQuery>();
        services.AddScoped<ICartStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICheckoutStore>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddSingleton(adminAuthOptions);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(googleAuthOptions);
        services.AddSingleton(twoFactorOptions);
        services.AddSingleton(customerAccountLifecycleOptions);
        services.AddSingleton(emailDeliveryOptions);
        services.AddSingleton(mediaStorageOptions);
        services.AddSingleton<MediaImageProcessor>();
        services.AddSingleton<IMediaMalwareScanner, NoOpMediaMalwareScanner>();
        services.AddSingleton<IMediaObjectStore>(_ =>
        {
            if (string.Equals(mediaStorageOptions.Provider, "S3", StringComparison.OrdinalIgnoreCase))
            {
                return new S3MediaObjectStore(mediaStorageOptions);
            }

            var configuredRoot = mediaStorageOptions.LocalRootPath;
            var localRoot = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(environment.ContentRootPath, "wwwroot", "media")
                : Path.GetFullPath(configuredRoot);
            return new LocalMediaObjectStore(localRoot);
        });
        services.AddScoped<WorkspaceEcommerce.Application.Abstractions.Media.IMediaStorageService, DurableMediaStorageService>();
        services.AddSingleton(configuration.GetSection(LoyaltyOptions.SectionName).Get<LoyaltyOptions>() ?? new LoyaltyOptions());
        services.Configure<VNPayOptions>(configuration.GetSection(VNPayOptions.SectionName));
        services.AddSingleton<IVNPayPaymentService, VNPayPaymentService>();
        services.AddSingleton<IAdminCredentialValidator, ConfiguredAdminCredentialValidator>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IGoogleJwtValidator, GoogleApiJwtValidator>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<ITotpService, OtpNetTotpService>();
        services.AddSingleton<ITwoFactorSecretProtector, DataProtectionTwoFactorSecretProtector>();
        services.AddScoped<ICustomerEmailOutbox, CustomerEmailOutbox>();
        services.AddScoped<CustomerEmailOutboxPayloadReader>();
        if (string.Equals(emailDeliveryOptions.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICustomerEmailDeliveryService, SmtpCustomerEmailDeliveryService>();
        }
        else
        {
            services.AddSingleton<ICustomerEmailDeliveryService, LoggingCustomerEmailDeliveryService>();
        }
        services.AddHostedService<CustomerEmailOutboxWorker>();
        services.AddHostedService<OutboxMetricsWorker>();
        services.AddHostedService<CustomerAccountCleanupWorker>();
        services.AddHostedService<MediaAssetCleanupWorker>();

        var miniLogisticsOptions = configuration
            .GetSection(MiniLogisticsOptions.SectionName)
            .Get<MiniLogisticsOptions>() ?? new MiniLogisticsOptions();
        services.Configure<MiniLogisticsOptions>(configuration.GetSection(MiniLogisticsOptions.SectionName));
        services.AddSingleton<MiniLogisticsFailureGate>();
        services.AddHttpClient<IShipmentService, MiniLogisticsClient>(client =>
        {
            client.BaseAddress = new Uri(miniLogisticsOptions.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", miniLogisticsOptions.ApiKey);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddHostedService<ShipmentCommandWorker>();

        return services;
    }
}
