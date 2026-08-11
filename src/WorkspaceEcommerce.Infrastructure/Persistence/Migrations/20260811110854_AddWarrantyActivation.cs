using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "warranty");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Additive, deterministic backfill. Ambiguous historical rows stay
            // null and cannot be activated until a trusted completion exists.
            migrationBuilder.Sql("""
                UPDATE ordering.orders AS orders
                SET completed_at = history.completed_at
                FROM (
                    SELECT order_id, MIN(changed_at) AS completed_at
                    FROM ordering.order_status_history
                    WHERE to_status = 'Completed'
                    GROUP BY order_id
                ) AS history
                WHERE orders.id = history.order_id
                  AND orders.completed_at IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "warranty_import_batches",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    imported_rows = table.Column<int>(type: "integer", nullable: false),
                    failed_rows = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "warranty_plans",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    activation_window_days = table.Column<int>(type: "integer", nullable: false),
                    terms_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "serialized_product_units",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identifier_key_version = table.Column<int>(type: "integer", nullable: false),
                    identifier_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    masked_identifier = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serialized_product_units", x => x.id);
                    table.ForeignKey(
                        name: "FK_serialized_product_units_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalSchema: "ordering",
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_serialized_product_units_product_variants_product_variant_id",
                        column: x => x.product_variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_serialized_product_units_warranty_import_batches_import_bat~",
                        column: x => x.import_batch_id,
                        principalSchema: "warranty",
                        principalTable: "warranty_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_warranty_plans",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warranty_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variant_warranty_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_variant_warranty_plans_product_variants_product_var~",
                        column: x => x.product_variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_variant_warranty_plans_warranty_plans_warranty_plan~",
                        column: x => x.warranty_plan_id,
                        principalSchema: "warranty",
                        principalTable: "warranty_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warranty_plan_coverages",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warranty_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    duration_months = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_plan_coverages", x => x.id);
                    table.ForeignKey(
                        name: "FK_warranty_plan_coverages_warranty_plans_warranty_plan_id",
                        column: x => x.warranty_plan_id,
                        principalSchema: "warranty",
                        principalTable: "warranty_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warranty_entitlements",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serialized_product_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warranty_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eligible_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activation_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    activation_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    accepted_terms_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    replacement_serialized_product_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_entitlements", x => x.id);
                    table.ForeignKey(
                        name: "FK_warranty_entitlements_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_entitlements_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalSchema: "ordering",
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_entitlements_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_entitlements_serialized_product_units_serialized_p~",
                        column: x => x.serialized_product_unit_id,
                        principalSchema: "warranty",
                        principalTable: "serialized_product_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_entitlements_warranty_plans_warranty_plan_id",
                        column: x => x.warranty_plan_id,
                        principalSchema: "warranty",
                        principalTable: "warranty_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warranty_audit_events",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warranty_entitlement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    serialized_product_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_warranty_audit_events_serialized_product_units_serialized_p~",
                        column: x => x.serialized_product_unit_id,
                        principalSchema: "warranty",
                        principalTable: "serialized_product_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warranty_audit_events_warranty_entitlements_warranty_entitl~",
                        column: x => x.warranty_entitlement_id,
                        principalSchema: "warranty",
                        principalTable: "warranty_entitlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warranty_coverage_snapshots",
                schema: "warranty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warranty_entitlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    duration_months = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranty_coverage_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_warranty_coverage_snapshots_warranty_entitlements_warranty_~",
                        column: x => x.warranty_entitlement_id,
                        principalSchema: "warranty",
                        principalTable: "warranty_entitlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_completed_at",
                schema: "ordering",
                table: "orders",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "ix_variant_warranty_plan_plan_effective",
                schema: "warranty",
                table: "product_variant_warranty_plans",
                columns: new[] { "warranty_plan_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ux_variant_warranty_plan_effective",
                schema: "warranty",
                table: "product_variant_warranty_plans",
                columns: new[] { "product_variant_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warranty_units_import_batch_id",
                schema: "warranty",
                table: "serialized_product_units",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_warranty_units_order_item_id",
                schema: "warranty",
                table: "serialized_product_units",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_warranty_units_variant_status",
                schema: "warranty",
                table: "serialized_product_units",
                columns: new[] { "product_variant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_warranty_units_identifier_fingerprint",
                schema: "warranty",
                table: "serialized_product_units",
                columns: new[] { "identifier_type", "identifier_key_version", "identifier_fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warranty_audit_entitlement_occurred_id",
                schema: "warranty",
                table: "warranty_audit_events",
                columns: new[] { "warranty_entitlement_id", "occurred_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_warranty_audit_unit_occurred_id",
                schema: "warranty",
                table: "warranty_audit_events",
                columns: new[] { "serialized_product_unit_id", "occurred_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_warranty_coverage_snapshots_entitlement_sort_id",
                schema: "warranty",
                table: "warranty_coverage_snapshots",
                columns: new[] { "warranty_entitlement_id", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_warranty_entitlements_customer_activated_id",
                schema: "warranty",
                table: "warranty_entitlements",
                columns: new[] { "customer_id", "activated_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_warranty_entitlements_order_id",
                schema: "warranty",
                table: "warranty_entitlements",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_warranty_entitlements_order_item_id",
                schema: "warranty",
                table: "warranty_entitlements",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_warranty_entitlements_replacement_unit_id",
                schema: "warranty",
                table: "warranty_entitlements",
                column: "replacement_serialized_product_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_warranty_entitlements_status_deadline",
                schema: "warranty",
                table: "warranty_entitlements",
                columns: new[] { "status", "activation_deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_warranty_entitlements_warranty_plan_id",
                schema: "warranty",
                table: "warranty_entitlements",
                column: "warranty_plan_id");

            migrationBuilder.CreateIndex(
                name: "ux_warranty_entitlements_unit_id",
                schema: "warranty",
                table: "warranty_entitlements",
                column: "serialized_product_unit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warranty_import_batches_created_id",
                schema: "warranty",
                table: "warranty_import_batches",
                columns: new[] { "created_at", "id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "ux_warranty_import_batches_checksum",
                schema: "warranty",
                table: "warranty_import_batches",
                column: "content_checksum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_warranty_plan_coverages_plan_component",
                schema: "warranty",
                table: "warranty_plan_coverages",
                columns: new[] { "warranty_plan_id", "component_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warranty_plans_active_effective_code",
                schema: "warranty",
                table: "warranty_plans",
                columns: new[] { "is_active", "effective_from", "code" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ux_warranty_plans_code",
                schema: "warranty",
                table: "warranty_plans",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_variant_warranty_plans",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "warranty_audit_events",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "warranty_coverage_snapshots",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "warranty_plan_coverages",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "warranty_entitlements",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "serialized_product_units",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "warranty_plans",
                schema: "warranty");

            migrationBuilder.DropTable(
                name: "warranty_import_batches",
                schema: "warranty");

            migrationBuilder.DropIndex(
                name: "ix_orders_completed_at",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "ordering",
                table: "orders");
        }
    }
}
