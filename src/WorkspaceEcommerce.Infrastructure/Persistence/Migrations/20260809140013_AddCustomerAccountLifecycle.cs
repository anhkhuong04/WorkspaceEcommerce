using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAccountLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_tokens",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_tokens_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    protected_payload = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token_families",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token_families", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_token_families_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_token_families_family_id",
                        column: x => x.family_id,
                        principalSchema: "customer",
                        principalTable: "refresh_token_families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_account_tokens_cleanup",
                schema: "customer",
                table: "account_tokens",
                columns: new[] { "expires_at", "consumed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_account_tokens_customer_purpose_expiry",
                schema: "customer",
                table: "account_tokens",
                columns: new[] { "customer_id", "purpose", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_customer_account_tokens_token_hash",
                schema: "customer",
                table: "account_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_email_outbox_cleanup",
                schema: "customer",
                table: "email_outbox",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_customer_email_outbox_due",
                schema: "customer",
                table: "email_outbox",
                columns: new[] { "sent_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_refresh_token_families_cleanup",
                schema: "customer",
                table: "refresh_token_families",
                columns: new[] { "revoked_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_refresh_token_families_customer_expiry",
                schema: "customer",
                table: "refresh_token_families",
                columns: new[] { "customer_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_refresh_tokens_cleanup",
                schema: "customer",
                table: "refresh_tokens",
                columns: new[] { "expires_at", "used_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_refresh_tokens_family_expiry",
                schema: "customer",
                table: "refresh_tokens",
                columns: new[] { "family_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_customer_refresh_tokens_token_hash",
                schema: "customer",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_tokens",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "email_outbox",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "refresh_token_families",
                schema: "customer");
        }
    }
}
