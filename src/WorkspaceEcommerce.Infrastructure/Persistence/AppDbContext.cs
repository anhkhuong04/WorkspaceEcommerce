using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
