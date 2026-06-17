using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customer");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id",
                schema: "ordering",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone_number",
                schema: "customer",
                table: "customers",
                column: "phone_number");

            migrationBuilder.CreateIndex(
                name: "ux_customers_email",
                schema: "customer",
                table: "customers",
                column: "email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_carts_customers_customer_id",
                schema: "cart",
                table: "carts",
                column: "customer_id",
                principalSchema: "customer",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_customer_id",
                schema: "ordering",
                table: "orders",
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
                name: "FK_carts_customers_customer_id",
                schema: "cart",
                table: "carts");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_customer_id",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "customer");

            migrationBuilder.DropIndex(
                name: "ix_orders_customer_id",
                schema: "ordering",
                table: "orders");
        }
    }
}
