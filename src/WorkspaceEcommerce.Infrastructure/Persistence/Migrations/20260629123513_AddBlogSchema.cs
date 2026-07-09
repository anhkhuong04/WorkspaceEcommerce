using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blog_posts",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    slug = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blog_posts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blog_comments",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blog_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    author_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blog_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_blog_comments_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalSchema: "content",
                        principalTable: "blog_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blog_post_related_products",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blog_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blog_post_related_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_blog_post_related_products_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalSchema: "content",
                        principalTable: "blog_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_blog_post_related_products_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blog_comments_blog_post_id",
                schema: "content",
                table: "blog_comments",
                column: "blog_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_related_products_blog_post_id_product_id",
                schema: "content",
                table: "blog_post_related_products",
                columns: new[] { "blog_post_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blog_post_related_products_product_id",
                schema: "content",
                table: "blog_post_related_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_is_published",
                schema: "content",
                table: "blog_posts",
                column: "is_published");

            migrationBuilder.CreateIndex(
                name: "ix_blog_posts_slug",
                schema: "content",
                table: "blog_posts",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blog_comments",
                schema: "content");

            migrationBuilder.DropTable(
                name: "blog_post_related_products",
                schema: "content");

            migrationBuilder.DropTable(
                name: "blog_posts",
                schema: "content");
        }
    }
}
