using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceShippingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "provider_shipment_id",
                schema: "shipping",
                table: "order_shipments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "actual_cost_amount",
                schema: "shipping",
                table: "order_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rate_plans",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    catalog_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origin_province_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_plans", x => x.id);
                    table.CheckConstraint("ck_shipping_rate_plan_revision", "revision > 0");
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_charge = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.id);
                    table.CheckConstraint("ck_shipping_quote_method", "method IN ('WorkspaceEC', 'MiniLogistics')");
                    table.CheckConstraint("ck_shipping_quote_values", "customer_charge >= 0 AND weight_grams > 0 AND revision > 0 AND expires_at_utc > created_at_utc");
                    table.ForeignKey(
                        name: "FK_quotes_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotes_rate_plans_rate_plan_id",
                        column: x => x.rate_plan_id,
                        principalSchema: "shipping",
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_audits",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    changes_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_rate_audits_rate_plans_rate_plan_id",
                        column: x => x.rate_plan_id,
                        principalSchema: "shipping",
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_zones",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    base_weight_grams = table.Column<int>(type: "integer", nullable: false),
                    base_fee = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    step_weight_grams = table.Column<int>(type: "integer", nullable: false),
                    step_fee = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    maximum_weight_grams = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_zones", x => x.id);
                    table.UniqueConstraint("AK_rate_zones_id_rate_plan_id", x => new { x.id, x.rate_plan_id });
                    table.CheckConstraint("ck_shipping_zone_fees", "base_fee >= 0 AND step_fee >= 0");
                    table.CheckConstraint("ck_shipping_zone_weight", "base_weight_grams > 0 AND step_weight_grams > 0 AND (maximum_weight_grams IS NULL OR maximum_weight_grams >= base_weight_grams)");
                    table.ForeignKey(
                        name: "FK_rate_zones_rate_plans_rate_plan_id",
                        column: x => x.rate_plan_id,
                        principalSchema: "shipping",
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_selections",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_charge = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    weight_grams = table.Column<int>(type: "integer", nullable: false),
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_selections", x => x.id);
                    table.CheckConstraint("ck_shipping_selection_method", "method IN ('WorkspaceEC', 'MiniLogistics')");
                    table.CheckConstraint("ck_shipping_selection_values", "customer_charge >= 0 AND weight_grams > 0");
                    table.ForeignKey(
                        name: "FK_order_selections_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_selections_quotes_quote_id",
                        column: x => x.quote_id,
                        principalSchema: "shipping",
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_selections_rate_plans_rate_plan_id",
                        column: x => x.rate_plan_id,
                        principalSchema: "shipping",
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zone_provinces",
                schema: "shipping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_provinces", x => x.id);
                    table.ForeignKey(
                        name: "FK_zone_provinces_rate_zones_zone_id_rate_plan_id",
                        columns: x => new { x.zone_id, x.rate_plan_id },
                        principalSchema: "shipping",
                        principalTable: "rate_zones",
                        principalColumns: new[] { "id", "rate_plan_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipment_actual_cost",
                schema: "shipping",
                table: "order_shipments",
                sql: "actual_cost_amount IS NULL OR actual_cost_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipment_provider_identity",
                schema: "shipping",
                table: "order_shipments",
                sql: "(provider = 'WorkspaceEC' AND provider_shipment_id IS NULL) OR (provider <> 'WorkspaceEC' AND provider_shipment_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_order_selections_order_id",
                schema: "shipping",
                table: "order_selections",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_selections_quote_id",
                schema: "shipping",
                table: "order_selections",
                column: "quote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_selections_rate_plan_id",
                schema: "shipping",
                table: "order_selections",
                column: "rate_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_expires_at_utc",
                schema: "shipping",
                table: "quotes",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_order_id",
                schema: "shipping",
                table: "quotes",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_rate_plan_id",
                schema: "shipping",
                table: "quotes",
                column: "rate_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_rate_audits_rate_plan_id_revision",
                schema: "shipping",
                table: "rate_audits",
                columns: new[] { "rate_plan_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipping_rate_plan_created",
                schema: "shipping",
                table: "rate_plans",
                columns: new[] { "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_shipping_rate_plan_published",
                schema: "shipping",
                table: "rate_plans",
                column: "status",
                unique: true,
                filter: "status = 'Published'");

            migrationBuilder.CreateIndex(
                name: "IX_rate_zones_rate_plan_id_code",
                schema: "shipping",
                table: "rate_zones",
                columns: new[] { "rate_plan_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zone_provinces_zone_id_rate_plan_id",
                schema: "shipping",
                table: "zone_provinces",
                columns: new[] { "zone_id", "rate_plan_id" });

            migrationBuilder.CreateIndex(
                name: "ux_shipping_zone_plan_province",
                schema: "shipping",
                table: "zone_provinces",
                columns: new[] { "rate_plan_id", "province_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never discard accepted shipping facts or manufacture provider IDs.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM shipping.rate_plans)
                       OR EXISTS (SELECT 1 FROM shipping.quotes)
                       OR EXISTS (SELECT 1 FROM shipping.order_shipments
                                  WHERE provider = 'WorkspaceEC' OR actual_cost_amount IS NOT NULL)
                    THEN RAISE EXCEPTION 'Shipping data exists. Disable new intake and deploy a forward fix; destructive rollback is not supported.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "order_selections",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "rate_audits",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "zone_provinces",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "quotes",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "rate_zones",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "rate_plans",
                schema: "shipping");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shipment_actual_cost",
                schema: "shipping",
                table: "order_shipments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shipment_provider_identity",
                schema: "shipping",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "actual_cost_amount",
                schema: "shipping",
                table: "order_shipments");

            migrationBuilder.AlterColumn<Guid>(
                name: "provider_shipment_id",
                schema: "shipping",
                table: "order_shipments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
