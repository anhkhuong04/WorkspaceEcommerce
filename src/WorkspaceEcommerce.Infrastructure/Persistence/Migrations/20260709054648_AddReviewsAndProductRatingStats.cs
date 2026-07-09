using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsAndProductRatingStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "average_rating",
                schema: "catalog",
                table: "products",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "review_count",
                schema: "catalog",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "reviews",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_customer_id",
                schema: "catalog",
                table: "reviews",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_product_id",
                schema: "catalog",
                table: "reviews",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_reviews_product_customer",
                schema: "catalog",
                table: "reviews",
                columns: new[] { "product_id", "customer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "average_rating",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "review_count",
                schema: "catalog",
                table: "products");
        }
    }
}
