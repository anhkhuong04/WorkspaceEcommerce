using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "promotions");

            migrationBuilder.AddColumn<string>(
                name: "coupon_code_snapshot",
                schema: "ordering",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "coupon_id",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "coupon_name_snapshot",
                schema: "ordering",
                table: "orders",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coupons",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    discount_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    minimum_subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "coupon_product_targets",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon_product_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_coupon_product_targets_coupons_coupon_id",
                        column: x => x.coupon_id,
                        principalSchema: "promotions",
                        principalTable: "coupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_coupon_product_targets_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coupon_redemptions",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon_redemptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_coupons_coupon_id",
                        column: x => x.coupon_id,
                        principalSchema: "promotions",
                        principalTable: "coupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_coupon_id",
                schema: "ordering",
                table: "orders",
                column: "coupon_id");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_product_targets_coupon_id",
                schema: "promotions",
                table: "coupon_product_targets",
                column: "coupon_id");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_product_targets_product_id",
                schema: "promotions",
                table: "coupon_product_targets",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_coupon_product_targets_coupon_id_product_id",
                schema: "promotions",
                table: "coupon_product_targets",
                columns: new[] { "coupon_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemptions_code_snapshot",
                schema: "promotions",
                table: "coupon_redemptions",
                column: "code_snapshot");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemptions_coupon_id",
                schema: "promotions",
                table: "coupon_redemptions",
                column: "coupon_id");

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemptions_customer_id",
                schema: "promotions",
                table: "coupon_redemptions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ux_coupon_redemptions_order_id",
                schema: "promotions",
                table: "coupon_redemptions",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupons_ends_at",
                schema: "promotions",
                table: "coupons",
                column: "ends_at");

            migrationBuilder.CreateIndex(
                name: "ix_coupons_is_active",
                schema: "promotions",
                table: "coupons",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_coupons_starts_at",
                schema: "promotions",
                table: "coupons",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "ux_coupons_code",
                schema: "promotions",
                table: "coupons",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_coupons_coupon_id",
                schema: "ordering",
                table: "orders",
                column: "coupon_id",
                principalSchema: "promotions",
                principalTable: "coupons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_coupons_coupon_id",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropTable(
                name: "coupon_product_targets",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "coupon_redemptions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "coupons",
                schema: "promotions");

            migrationBuilder.DropIndex(
                name: "ix_orders_coupon_id",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "coupon_code_snapshot",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "coupon_id",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "coupon_name_snapshot",
                schema: "ordering",
                table: "orders");
        }
    }
}
