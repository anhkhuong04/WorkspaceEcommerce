using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemProductImageSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "product_image_url_snapshot",
                schema: "ordering",
                table: "order_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Historic orders did not persist a product image. Capture the current
            // primary gallery image once so existing receipts can include it while
            // future catalog edits remain isolated from the order snapshot.
            migrationBuilder.Sql("""
                UPDATE ordering.order_items AS order_item
                SET product_image_url_snapshot = primary_image.image_url
                FROM catalog.product_variants AS variant
                JOIN LATERAL (
                    SELECT image.image_url
                    FROM catalog.product_images AS image
                    WHERE image.product_id = variant.product_id
                    ORDER BY image.sort_order, image.id
                    LIMIT 1
                ) AS primary_image ON TRUE
                WHERE order_item.product_variant_id = variant.id
                  AND order_item.product_image_url_snapshot IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "product_image_url_snapshot",
                schema: "ordering",
                table: "order_items");
        }
    }
}
