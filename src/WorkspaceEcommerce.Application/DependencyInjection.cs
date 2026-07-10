using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceEcommerce.Application.Modules.Admin.Dashboard;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Blogs;
using WorkspaceEcommerce.Application.Modules.Cart;
using WorkspaceEcommerce.Application.Modules.Catalog.Categories;
using WorkspaceEcommerce.Application.Modules.Catalog.Products;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;
using WorkspaceEcommerce.Application.Modules.Content.Banners;
using WorkspaceEcommerce.Application.Modules.Coupons;
using WorkspaceEcommerce.Application.Modules.Customers.Addresses;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Orders;
using WorkspaceEcommerce.Application.Modules.Customers.Profile;
using WorkspaceEcommerce.Application.Modules.Loyalty;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Reviews;

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
        services.AddScoped<IStorefrontBannerService, StorefrontBannerService>();
        services.AddScoped<ICustomerAuthService, CustomerAuthService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();
        services.AddScoped<IStorefrontCartService, StorefrontCartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IStorefrontOrderLookupService, StorefrontOrderLookupService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<IAdminCouponService, AdminCouponService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IAdminBlogService, AdminBlogService>();
        services.AddScoped<IStorefrontBlogService, StorefrontBlogService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAdminReviewService, AdminReviewService>();

        return services;
    }
}
