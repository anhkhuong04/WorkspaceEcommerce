using Microsoft.EntityFrameworkCore.Migrations;
using WorkspaceEcommerce.Domain.Common;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultilingualSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<LocalizedText>(
                name: "name",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<LocalizedText>(
                name: "description",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "ordering",
                table: "orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "exchange_rate",
                schema: "ordering",
                table: "orders",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<LocalizedText>(
                name: "name",
                schema: "catalog",
                table: "categories",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "exchange_rate",
                schema: "ordering",
                table: "orders");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "catalog",
                table: "products",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(LocalizedText),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "catalog",
                table: "products",
                type: "text",
                nullable: true,
                oldClrType: typeof(LocalizedText),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "catalog",
                table: "categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(LocalizedText),
                oldType: "jsonb");
        }
    }
}
