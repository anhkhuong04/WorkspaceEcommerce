using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminOperationsRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_order_code",
                schema: "ordering",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "import_fingerprint",
                schema: "ordering",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "import_source",
                schema: "ordering",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_amount",
                schema: "ordering",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "return_decision",
                schema: "ordering",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "return_decision_reason",
                schema: "ordering",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "return_reviewed_at",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "points_debt",
                schema: "loyalty",
                table: "customer_loyalty_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "order_returns",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    receipt_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    merchandise_refund = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    shipping_refund = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refunded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    refund_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    refund_actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    refund_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_returns_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_return_audits",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_return_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_return_audits_order_returns_return_case_id",
                        column: x => x.return_case_id,
                        principalSchema: "ordering",
                        principalTable: "order_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_return_lines",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    received_quantity = table.Column<int>(type: "integer", nullable: false),
                    restock_quantity = table.Column<int>(type: "integer", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    inspection = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    serialized_unit_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_return_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_return_lines_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalSchema: "ordering",
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_return_lines_order_returns_return_case_id",
                        column: x => x.return_case_id,
                        principalSchema: "ordering",
                        principalTable: "order_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_orders_import_identity",
                schema: "ordering",
                table: "orders",
                columns: new[] { "import_source", "external_order_code" },
                unique: true,
                filter: "import_source IS NOT NULL AND external_order_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_order_return_audits_return_case_id_occurred_at",
                schema: "ordering",
                table: "order_return_audits",
                columns: new[] { "return_case_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_order_return_lines_order_item_id",
                schema: "ordering",
                table: "order_return_lines",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_return_lines_return_case_id_order_item_id",
                schema: "ordering",
                table: "order_return_lines",
                columns: new[] { "return_case_id", "order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_returns_order_id_refund_reference",
                schema: "ordering",
                table: "order_returns",
                columns: new[] { "order_id", "refund_reference" },
                unique: true,
                filter: "refund_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_order_returns_order_id_request_id",
                schema: "ordering",
                table: "order_returns",
                columns: new[] { "order_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_returns_status_created_at",
                schema: "ordering",
                table: "order_returns",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM ordering.order_returns)
                    OR EXISTS (SELECT 1 FROM ordering.orders WHERE import_source IS NOT NULL OR refunded_amount > 0)
                    OR EXISTS (SELECT 1 FROM loyalty.customer_loyalty_accounts WHERE points_debt > 0) THEN
                    RAISE EXCEPTION 'Return/import reconciliation evidence exists; preserve history and use a forward fix.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "order_return_audits",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_return_lines",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_returns",
                schema: "ordering");

            migrationBuilder.DropIndex(
                name: "ux_orders_import_identity",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "external_order_code",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "import_fingerprint",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "import_source",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "refunded_amount",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "return_decision",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "return_decision_reason",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "return_reviewed_at",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "points_debt",
                schema: "loyalty",
                table: "customer_loyalty_accounts");
        }
    }
}
