using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrepaidOrderDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_due_at",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "released_at",
                schema: "promotions",
                table: "coupon_redemptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_payment_due_at_status",
                schema: "ordering",
                table: "orders",
                columns: new[] { "payment_due_at", "status" },
                filter: "payment_due_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_payment_due_at_status",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_due_at",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "released_at",
                schema: "promotions",
                table: "coupon_redemptions");
        }
    }
}
