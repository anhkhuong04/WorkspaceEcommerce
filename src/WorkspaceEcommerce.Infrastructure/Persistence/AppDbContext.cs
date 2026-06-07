using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext, ICartStore
{
    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();

    IQueryable<Category> IAppDbContext.Categories => Categories;

    IQueryable<Product> IAppDbContext.Products => Products;

    IQueryable<ProductVariant> IAppDbContext.ProductVariants => ProductVariants;

    IQueryable<ProductImage> IAppDbContext.ProductImages => ProductImages;

    IQueryable<ProductSpecification> IAppDbContext.ProductSpecifications => ProductSpecifications;

    async Task<Cart?> ICartStore.FindCartBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.SessionId == sessionId, cancellationToken);
    }

    async Task<ProductVariant?> ICartStore.FindProductVariantByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ProductVariants
            .FirstOrDefaultAsync(variant => variant.Id == id, cancellationToken);
    }

    async Task<Product?> ICartStore.FindProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await Products
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    async Task<Category?> ICartStore.FindCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await Categories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    void IAppDbContext.Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Add(entity);
    }

    void IAppDbContext.Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Update(entity);
    }

    void IAppDbContext.Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Remove(entity);
    }

    void ICartStore.Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Add(entity);
    }

    void ICartStore.Update<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Update(entity);
    }

    void ICartStore.Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        Set<TEntity>().Remove(entity);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
