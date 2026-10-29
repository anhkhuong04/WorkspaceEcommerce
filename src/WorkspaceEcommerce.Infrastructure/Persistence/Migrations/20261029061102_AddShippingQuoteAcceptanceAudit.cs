using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingQuoteAcceptanceAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "checkout_request_hash",
                schema: "shipping",
                table: "quotes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revocation_actor",
                schema: "shipping",
                table: "quotes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revocation_reason",
                schema: "shipping",
                table: "quotes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM shipping.quotes WHERE checkout_request_hash IS NOT NULL OR revocation_actor IS NOT NULL OR revocation_reason IS NOT NULL) THEN
                    RAISE EXCEPTION 'Shipping acceptance/revocation audit exists; disable new intake and use a forward fix.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "checkout_request_hash",
                schema: "shipping",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "revocation_actor",
                schema: "shipping",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "revocation_reason",
                schema: "shipping",
                table: "quotes");
        }
    }
}
