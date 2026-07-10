using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                schema: "ordering",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.Sql(
                "UPDATE ordering.orders SET payment_status = 'Pending' WHERE payment_method = 'ManualBankTransfer';");

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    txn_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gateway_transaction_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gateway_response_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    gateway_response_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    secure_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    raw_response = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_payment_status",
                schema: "ordering",
                table: "orders",
                column: "payment_status");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_order_id",
                schema: "payments",
                table: "payment_transactions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ux_payment_transactions_txn_ref",
                schema: "payments",
                table: "payment_transactions",
                column: "txn_ref",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "payments");

            migrationBuilder.DropIndex(
                name: "ix_orders_payment_status",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paid_at",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_status",
                schema: "ordering",
                table: "orders");
        }
    }
}
