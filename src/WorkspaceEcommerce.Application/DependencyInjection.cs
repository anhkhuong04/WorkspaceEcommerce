using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Catalog.Categories;
using WorkspaceEcommerce.Application.Modules.Catalog.Products;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;

namespace WorkspaceEcommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminCategoryService, AdminCategoryService>();
        services.AddScoped<IAdminProductService, AdminProductService>();
        services.AddScoped<IStorefrontCatalogService, StorefrontCatalogService>();

        return services;
    }
}
