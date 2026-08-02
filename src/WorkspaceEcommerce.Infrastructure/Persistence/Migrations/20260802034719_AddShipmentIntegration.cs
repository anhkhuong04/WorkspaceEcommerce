using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shipping");

            migrationBuilder.CreateTable(
                name: "order_shipments",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shipping_fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_synced_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_event_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_shipments", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_shipments_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_command_outbox",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_command_outbox", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_command_outbox_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipment_event_inbox",
                schema: "shipping",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tracking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_event_inbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_timeline_entries",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_timeline_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_timeline_entries_order_shipments_order_shipment_id",
                        column: x => x.order_shipment_id,
                        principalSchema: "shipping",
                        principalTable: "order_shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_orders_tracking_code",
                schema: "ordering",
                table: "orders",
                column: "tracking_code",
                unique: true,
                filter: "tracking_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_order_shipments_provider_status",
                schema: "shipping",
                table: "order_shipments",
                column: "provider_status");

            migrationBuilder.CreateIndex(
                name: "ux_order_shipments_order_id",
                schema: "shipping",
                table: "order_shipments",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_order_shipments_provider_shipment_id",
                schema: "shipping",
                table: "order_shipments",
                columns: new[] { "provider", "provider_shipment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_order_shipments_tracking_code",
                schema: "shipping",
                table: "order_shipments",
                column: "tracking_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipment_command_outbox_due",
                schema: "shipping",
                table: "shipment_command_outbox",
                columns: new[] { "completed_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_shipment_command_outbox_active_order_type",
                schema: "shipping",
                table: "shipment_command_outbox",
                columns: new[] { "order_id", "command_type" },
                unique: true,
                filter: "completed_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shipment_event_inbox_received_at_utc",
                schema: "shipping",
                table: "shipment_event_inbox",
                column: "received_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_shipment_timeline_provider_event_id",
                schema: "shipping",
                table: "shipment_timeline_entries",
                column: "provider_event_id",
                unique: true,
                filter: "provider_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_shipment_timeline_state_time",
                schema: "shipping",
                table: "shipment_timeline_entries",
                columns: new[] { "order_shipment_id", "changed_at_utc", "provider_status" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_command_outbox",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "shipment_event_inbox",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "shipment_timeline_entries",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "order_shipments",
                schema: "shipping");

            migrationBuilder.DropIndex(
                name: "ux_orders_tracking_code",
                schema: "ordering",
                table: "orders");
        }
    }
}
