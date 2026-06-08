using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Cart;
using WorkspaceEcommerce.Application.Modules.Catalog.Categories;
using WorkspaceEcommerce.Application.Modules.Catalog.Products;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;
using WorkspaceEcommerce.Application.Modules.Content.Banners;
using WorkspaceEcommerce.Application.Modules.Ordering;

namespace WorkspaceEcommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminCategoryService, AdminCategoryService>();
        services.AddScoped<IAdminProductService, AdminProductService>();
        services.AddScoped<IStorefrontCatalogService, StorefrontCatalogService>();
        services.AddScoped<IAdminBannerService, AdminBannerService>();
        services.AddScoped<IStorefrontCartService, StorefrontCartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IStorefrontOrderLookupService, StorefrontOrderLookupService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();

        return services;
    }
}
