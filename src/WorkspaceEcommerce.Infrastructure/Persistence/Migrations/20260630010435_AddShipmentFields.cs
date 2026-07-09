using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "height_cm",
                schema: "catalog",
                table: "product_variants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "length_cm",
                schema: "catalog",
                table: "product_variants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                schema: "catalog",
                table: "product_variants",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "width_cm",
                schema: "catalog",
                table: "product_variants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shipment_id",
                schema: "ordering",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tracking_code",
                schema: "ordering",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "height_cm",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "length_cm",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "width_cm",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "shipment_id",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tracking_code",
                schema: "ordering",
                table: "orders");
        }
    }
}
