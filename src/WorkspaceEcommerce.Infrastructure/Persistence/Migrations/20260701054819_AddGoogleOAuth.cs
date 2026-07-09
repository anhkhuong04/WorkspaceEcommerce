using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                schema: "customer",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                schema: "customer",
                table: "customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                schema: "customer",
                table: "customers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_id",
                schema: "customer",
                table: "customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_email_verified",
                schema: "customer",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "reward_points",
                schema: "customer",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                schema: "customer",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                schema: "customer",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "addresses",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    street = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ward = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    province = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "login_history",
                schema: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_customers_google_id",
                schema: "customer",
                table: "customers",
                column: "google_id",
                unique: true,
                filter: "google_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_addresses_customer_id",
                schema: "customer",
                table: "addresses",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_login_history_customer_id",
                schema: "customer",
                table: "login_history",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_login_history_login_time",
                schema: "customer",
                table: "login_history",
                column: "login_time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "addresses",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "login_history",
                schema: "customer");

            migrationBuilder.DropIndex(
                name: "ux_customers_google_id",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "avatar_url",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "google_id",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_email_verified",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "reward_points",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                schema: "customer",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                schema: "customer",
                table: "customers");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                schema: "customer",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                schema: "customer",
                table: "customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
