using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditedFullRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payment_receipt_kind",
                schema: "payments",
                table: "order_payment_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payment_receipt_kind",
                schema: "payments",
                table: "order_payment_receipts",
                sql: "kind IN ('ManualTransfer', 'CodCollected', 'CodRemitted', 'FullRefund')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payment_receipt_kind",
                schema: "payments",
                table: "order_payment_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payment_receipt_kind",
                schema: "payments",
                table: "order_payment_receipts",
                sql: "kind IN ('ManualTransfer', 'CodCollected', 'CodRemitted')");
        }
    }
}
