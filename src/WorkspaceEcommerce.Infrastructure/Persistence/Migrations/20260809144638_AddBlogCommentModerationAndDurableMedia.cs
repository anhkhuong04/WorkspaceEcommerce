using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogCommentModerationAndDurableMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "moderated_at",
                schema: "content",
                table: "blog_comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderated_by",
                schema: "content",
                table: "blog_comments",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                schema: "content",
                table: "blog_comments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE content.blog_comments
                SET moderation_status = CASE WHEN is_approved THEN 'Approved' ELSE 'Pending' END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "moderation_status",
                schema: "content",
                table: "blog_comments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "is_approved",
                schema: "content",
                table: "blog_comments");

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    public_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    frame_count = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "media_asset_variants",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    public_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_asset_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_asset_variants_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalSchema: "content",
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blog_comments_post_moderation_created",
                schema: "content",
                table: "blog_comments",
                columns: new[] { "blog_post_id", "moderation_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_media_asset_variants_media_asset_id_name",
                schema: "content",
                table: "media_asset_variants",
                columns: new[] { "media_asset_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_object_key",
                schema: "content",
                table: "media_assets",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_public_url",
                schema: "content",
                table: "media_assets",
                column: "public_url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_state_created_at",
                schema: "content",
                table: "media_assets",
                columns: new[] { "state", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_asset_variants",
                schema: "content");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "content");

            migrationBuilder.DropIndex(
                name: "ix_blog_comments_post_moderation_created",
                schema: "content",
                table: "blog_comments");

            migrationBuilder.DropColumn(
                name: "moderated_at",
                schema: "content",
                table: "blog_comments");

            migrationBuilder.DropColumn(
                name: "moderated_by",
                schema: "content",
                table: "blog_comments");

            migrationBuilder.AddColumn<bool>(
                name: "is_approved",
                schema: "content",
                table: "blog_comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE content.blog_comments
                SET is_approved = moderation_status = 'Approved';
                """);

            migrationBuilder.DropColumn(
                name: "moderation_status",
                schema: "content",
                table: "blog_comments");
        }
    }
}
