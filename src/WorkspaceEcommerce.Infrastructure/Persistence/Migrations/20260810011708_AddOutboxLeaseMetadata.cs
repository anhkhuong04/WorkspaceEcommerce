using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxLeaseMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shipment_command_outbox_due",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropIndex(
                name: "ux_shipment_command_outbox_active_order_type",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_at_utc",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempt_at_utc",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error_category",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at_utc",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_owner",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "shipping",
                table: "shipment_command_outbox",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_at",
                schema: "customer",
                table: "email_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                schema: "customer",
                table: "email_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_owner",
                schema: "customer",
                table: "email_outbox",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                schema: "customer",
                table: "email_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "customer",
                table: "email_outbox",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // Existing terminal rows must remain terminal before the new active
            // partial unique index is created. Without this backfill, a historic
            // completed command could be treated as active and block retries.
            migrationBuilder.Sql("""
                UPDATE shipping.shipment_command_outbox
                SET status = CASE
                    WHEN completed_at_utc IS NULL THEN 'Pending'
                    ELSE 'Completed'
                END;
                """);

            migrationBuilder.Sql("""
                UPDATE customer.email_outbox
                SET status = CASE
                    WHEN sent_at IS NULL THEN 'Pending'
                    ELSE 'Sent'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_shipment_command_outbox_claim",
                schema: "shipping",
                table: "shipment_command_outbox",
                columns: new[] { "status", "next_attempt_at_utc", "lease_expires_at_utc", "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_shipment_command_outbox_active_order_type",
                schema: "shipping",
                table: "shipment_command_outbox",
                columns: new[] { "order_id", "command_type" },
                unique: true,
                filter: "status IN ('Pending', 'Leased')");

            migrationBuilder.CreateIndex(
                name: "ix_customer_email_outbox_claim",
                schema: "customer",
                table: "email_outbox",
                columns: new[] { "sent_at", "status", "dead_lettered_at", "next_attempt_at", "lease_expires_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shipment_command_outbox_claim",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropIndex(
                name: "ux_shipment_command_outbox_active_order_type",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropIndex(
                name: "ix_customer_email_outbox_claim",
                schema: "customer",
                table: "email_outbox");

            migrationBuilder.DropColumn(
                name: "dead_lettered_at_utc",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "last_attempt_at_utc",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "last_error_category",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "lease_expires_at_utc",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "lease_owner",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "lease_token",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "shipping",
                table: "shipment_command_outbox");

            migrationBuilder.DropColumn(
                name: "dead_lettered_at",
                schema: "customer",
                table: "email_outbox");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                schema: "customer",
                table: "email_outbox");

            migrationBuilder.DropColumn(
                name: "lease_owner",
                schema: "customer",
                table: "email_outbox");

            migrationBuilder.DropColumn(
                name: "lease_token",
                schema: "customer",
                table: "email_outbox");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "customer",
                table: "email_outbox");

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
        }
    }
}
