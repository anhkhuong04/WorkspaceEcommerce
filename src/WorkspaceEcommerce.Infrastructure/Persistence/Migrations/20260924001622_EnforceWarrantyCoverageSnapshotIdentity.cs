using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWarrantyCoverageSnapshotIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM warranty.warranty_coverage_snapshots AS duplicate
                USING warranty.warranty_coverage_snapshots AS retained
                WHERE duplicate.warranty_entitlement_id = retained.warranty_entitlement_id
                  AND duplicate.component_code = retained.component_code
                  AND duplicate.id > retained.id
                  AND duplicate.display_name IS NOT DISTINCT FROM retained.display_name
                  AND duplicate.duration_months IS NOT DISTINCT FROM retained.duration_months
                  AND duplicate.starts_at IS NOT DISTINCT FROM retained.starts_at
                  AND duplicate.ends_at IS NOT DISTINCT FROM retained.ends_at
                  AND duplicate.sort_order IS NOT DISTINCT FROM retained.sort_order;

                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM warranty.warranty_coverage_snapshots
                        GROUP BY warranty_entitlement_id, component_code
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Conflicting warranty coverage snapshots must be reconciled before applying the unique identity constraint.';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_warranty_coverage_snapshots_entitlement_component",
                schema: "warranty",
                table: "warranty_coverage_snapshots",
                columns: new[] { "warranty_entitlement_id", "component_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_warranty_coverage_snapshots_entitlement_component",
                schema: "warranty",
                table: "warranty_coverage_snapshots");
        }
    }
}
