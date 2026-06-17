using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Cart;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Domain.Modules.Cart.Cart>
{
    public void Configure(EntityTypeBuilder<Domain.Modules.Cart.Cart> builder)
    {
        builder.ToTable("carts", "cart");

        builder.HasKey(cart => cart.Id);

        builder.Property(cart => cart.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(cart => cart.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(cart => cart.SessionId)
            .HasColumnName("session_id")
            .HasMaxLength(128);

        builder.Property(cart => cart.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(cart => cart.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(cart => cart.SessionId)
            .HasDatabaseName("ix_carts_session_id");

        builder.HasIndex(cart => cart.CustomerId)
            .HasDatabaseName("ix_carts_customer_id");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(cart => cart.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(cart => cart.Items)
            .WithOne()
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(cart => cart.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
