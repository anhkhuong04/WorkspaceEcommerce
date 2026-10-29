using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditedOrderPaymentReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "order_payment_receipts",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_receipts", x => x.id);
                    table.CheckConstraint("ck_order_payment_receipt_amount", "amount >= 0 AND currency = 'VND'");
                    table.CheckConstraint("ck_order_payment_receipt_kind", "kind IN ('ManualTransfer', 'CodCollected', 'CodRemitted')");
                    table.ForeignKey(
                        name: "FK_order_payment_receipts_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_receipts_kind_reference",
                schema: "payments",
                table: "order_payment_receipts",
                columns: new[] { "kind", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_receipts_order_id_kind",
                schema: "payments",
                table: "order_payment_receipts",
                columns: new[] { "order_id", "kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM payments.order_payment_receipts) THEN
                    RAISE EXCEPTION 'Payment receipts exist; preserve audit evidence and use a forward fix.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "order_payment_receipts",
                schema: "payments");
        }
    }
}
