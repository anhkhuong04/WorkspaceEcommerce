using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReadPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_reviews_created_id",
                schema: "catalog",
                table: "reviews",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_product_created_id",
                schema: "catalog",
                table: "reviews",
                columns: new[] { "product_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_products_category_active_slug",
                schema: "catalog",
                table: "products",
                columns: new[] { "category_id", "is_active", "slug" });

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product_active_price",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "product_id", "is_active", "price" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_created_order_code",
                schema: "ordering",
                table: "orders",
                columns: new[] { "customer_id", "created_at", "order_code" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_status_created_order_code",
                schema: "ordering",
                table: "orders",
                columns: new[] { "customer_id", "status", "created_at", "order_code" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_status_created_order_code",
                schema: "ordering",
                table: "orders",
                columns: new[] { "status", "created_at", "order_code" });

            migrationBuilder.CreateIndex(
                name: "ix_coupons_active_created_code",
                schema: "promotions",
                table: "coupons",
                columns: new[] { "is_active", "created_at", "code" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_published_at_id",
                schema: "content",
                table: "blog_posts",
                columns: new[] { "is_published", "published_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reviews_created_id",
                schema: "catalog",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_reviews_product_created_id",
                schema: "catalog",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_products_category_active_slug",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_variants_product_active_price",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ix_orders_customer_created_order_code",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_customer_status_created_order_code",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_status_created_order_code",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_coupons_active_created_code",
                schema: "promotions",
                table: "coupons");

            migrationBuilder.DropIndex(
                name: "ix_blog_posts_published_at_id",
                schema: "content",
                table: "blog_posts");
        }
    }
}
