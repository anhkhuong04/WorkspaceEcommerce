using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerTwoFactorAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "two_factor_secret",
                schema: "customer",
                table: "customers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "last_two_factor_time_step",
                schema: "customer",
                table: "customers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_two_factor_secret",
                schema: "customer",
                table: "customers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_setup_expires_at",
                schema: "customer",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            // Previous versions generated a random value and set the enabled flag without
            // proving enrollment. Those values are not Data Protection payloads and cannot
            // safely participate in the new authentication flow, so require re-enrollment.
            migrationBuilder.Sql("""
                UPDATE customer.customers
                SET two_factor_enabled = FALSE,
                    two_factor_secret = NULL,
                    pending_two_factor_secret = NULL,
                    two_factor_setup_expires_at = NULL,
                    last_two_factor_time_step = NULL
                WHERE two_factor_enabled = TRUE
                   OR two_factor_secret IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "two_factor_challenges",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_challenges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "two_factor_recovery_codes",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_two_factor_challenges_customer_expiry",
                schema: "customer",
                table: "two_factor_challenges",
                columns: new[] { "customer_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_two_factor_challenges_token_hash",
                schema: "customer",
                table: "two_factor_challenges",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_two_factor_recovery_codes_customer_used",
                schema: "customer",
                table: "two_factor_recovery_codes",
                columns: new[] { "customer_id", "used_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "two_factor_challenges",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "two_factor_recovery_codes",
                schema: "customer");

            migrationBuilder.DropColumn(
                name: "last_two_factor_time_step",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "pending_two_factor_secret",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "two_factor_setup_expires_at",
                schema: "customer",
                table: "customers");

            migrationBuilder.AlterColumn<string>(
                name: "two_factor_secret",
                schema: "customer",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);
        }
    }
}
