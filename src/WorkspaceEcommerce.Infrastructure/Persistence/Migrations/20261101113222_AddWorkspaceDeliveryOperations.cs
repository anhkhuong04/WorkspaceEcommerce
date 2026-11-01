using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceDeliveryOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assigned_to",
                schema: "shipping",
                table: "order_shipments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "shipping",
                table: "order_shipments",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "workspace_operations",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    assigned_to = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_operations", x => x.id);
                    table.CheckConstraint("ck_workspace_operation_revision", "revision > 0");
                    table.ForeignKey(
                        name: "FK_workspace_operations_order_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "shipping",
                        principalTable: "order_shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_operations_shipment_id_revision",
                schema: "shipping",
                table: "workspace_operations",
                columns: new[] { "shipment_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM shipping.workspace_operations) THEN
                    RAISE EXCEPTION 'Workspace delivery audit exists; preserve evidence and use a forward fix.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "workspace_operations",
                schema: "shipping");

            migrationBuilder.DropColumn(
                name: "assigned_to",
                schema: "shipping",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "shipping",
                table: "order_shipments");
        }
    }
}
