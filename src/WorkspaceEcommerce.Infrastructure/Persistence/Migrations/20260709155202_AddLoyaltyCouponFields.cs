using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyCouponFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "promotions",
                table: "coupons",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "promotions",
                table: "coupons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Admin");

            migrationBuilder.CreateIndex(
                name: "ix_coupons_customer_id",
                schema: "promotions",
                table: "coupons",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_coupons_source",
                schema: "promotions",
                table: "coupons",
                column: "source");

            migrationBuilder.AddForeignKey(
                name: "FK_coupons_customers_customer_id",
                schema: "promotions",
                table: "coupons",
                column: "customer_id",
                principalSchema: "customer",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_coupons_customers_customer_id",
                schema: "promotions",
                table: "coupons");

            migrationBuilder.DropIndex(
                name: "ix_coupons_customer_id",
                schema: "promotions",
                table: "coupons");

            migrationBuilder.DropIndex(
                name: "ix_coupons_source",
                schema: "promotions",
                table: "coupons");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "promotions",
                table: "coupons");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "promotions",
                table: "coupons");
        }
    }
}
