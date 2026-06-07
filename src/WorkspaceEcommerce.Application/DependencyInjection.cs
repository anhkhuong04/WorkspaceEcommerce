using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Modules.Catalog.Categories;

namespace WorkspaceEcommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAdminCategoryService, AdminCategoryService>();

        return services;
    }
}
