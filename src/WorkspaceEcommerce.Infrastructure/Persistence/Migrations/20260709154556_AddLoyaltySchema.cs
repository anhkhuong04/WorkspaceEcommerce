using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "loyalty");

            migrationBuilder.CreateTable(
                name: "customer_loyalty_accounts",
                schema: "loyalty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_points = table.Column<int>(type: "integer", nullable: false),
                    total_points_earned = table.Column<int>(type: "integer", nullable: false),
                    current_tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tier_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_loyalty_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_loyalty_accounts_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_tiers",
                schema: "loyalty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    min_total_points_earned = table.Column<int>(type: "integer", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    free_shipping_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_tiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_transactions",
                schema: "loyalty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_loyalty_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_loyalty_transactions_coupons_voucher_id",
                        column: x => x.voucher_id,
                        principalSchema: "promotions",
                        principalTable: "coupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loyalty_transactions_customer_loyalty_accounts_customer_loy~",
                        column: x => x.customer_loyalty_account_id,
                        principalSchema: "loyalty",
                        principalTable: "customer_loyalty_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loyalty_transactions_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "loyalty",
                table: "loyalty_tiers",
                columns: new[] { "id", "discount_percent", "free_shipping_enabled", "min_total_points_earned", "type" },
                values: new object[,]
                {
                    { new Guid("8d12c98b-10ce-4aac-8904-000000000001"), 0m, false, 0, "Bronze" },
                    { new Guid("8d12c98b-10ce-4aac-8904-000000000002"), 3m, false, 500, "Silver" },
                    { new Guid("8d12c98b-10ce-4aac-8904-000000000003"), 5m, true, 2000, "Gold" },
                    { new Guid("8d12c98b-10ce-4aac-8904-000000000004"), 10m, true, 5000, "Platinum" }
                });

            migrationBuilder.CreateIndex(
                name: "ux_customer_loyalty_accounts_customer_id",
                schema: "loyalty",
                table: "customer_loyalty_accounts",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loyalty_tiers_type",
                schema: "loyalty",
                table: "loyalty_tiers",
                column: "type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_account_id",
                schema: "loyalty",
                table: "loyalty_transactions",
                column: "customer_loyalty_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_created_at",
                schema: "loyalty",
                table: "loyalty_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_voucher_id",
                schema: "loyalty",
                table: "loyalty_transactions",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "ux_loyalty_transactions_earn_order",
                schema: "loyalty",
                table: "loyalty_transactions",
                column: "order_id",
                unique: true,
                filter: "\"type\" = 'Earn' AND order_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loyalty_tiers",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "loyalty_transactions",
                schema: "loyalty");

            migrationBuilder.DropTable(
                name: "customer_loyalty_accounts",
                schema: "loyalty");
        }
    }
}
