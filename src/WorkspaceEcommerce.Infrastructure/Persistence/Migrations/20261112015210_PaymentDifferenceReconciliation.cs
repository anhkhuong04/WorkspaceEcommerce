using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentDifferenceReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_payment_exceptions",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    actual_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    settlement = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    settlement_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    settlement_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    settlement_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    settled_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_payment_exceptions_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_exceptions_order_id_reference",
                schema: "payments",
                table: "order_payment_exceptions",
                columns: new[] { "order_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_exceptions_order_id_request_id",
                schema: "payments",
                table: "order_payment_exceptions",
                columns: new[] { "order_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_exceptions_settled_at",
                schema: "payments",
                table: "order_payment_exceptions",
                column: "settled_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM payments.order_payment_exceptions) THEN
                    RAISE EXCEPTION 'Payment difference evidence exists; preserve history and use a forward fix.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "order_payment_exceptions",
                schema: "payments");
        }
    }
}
