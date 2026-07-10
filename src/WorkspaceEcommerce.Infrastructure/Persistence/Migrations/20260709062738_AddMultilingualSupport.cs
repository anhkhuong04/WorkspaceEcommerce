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
            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.products
                ALTER COLUMN name TYPE jsonb
                USING jsonb_build_object('en', name);
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.products
                ALTER COLUMN description TYPE jsonb
                USING CASE
                    WHEN description IS NULL THEN NULL
                    ELSE jsonb_build_object('en', description)
                END;
                """);

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

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.categories
                ALTER COLUMN name TYPE jsonb
                USING jsonb_build_object('en', name);
                """);
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

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.products
                ALTER COLUMN name TYPE character varying(250)
                USING COALESCE(name->>'en', name->>'vi', '');
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.products
                ALTER COLUMN description TYPE text
                USING CASE
                    WHEN description IS NULL THEN NULL
                    ELSE COALESCE(description->>'en', description->>'vi')
                END;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.categories
                ALTER COLUMN name TYPE character varying(200)
                USING COALESCE(name->>'en', name->>'vi', '');
                """);
        }
    }
}
