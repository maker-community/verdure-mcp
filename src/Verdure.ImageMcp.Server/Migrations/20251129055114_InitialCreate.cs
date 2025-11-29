using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verdure.ImageMcp.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "image_generation_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    style = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    image_data = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email_sent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    hangfire_job_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_generation_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_is_active",
                table: "api_tokens",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_token_hash",
                table: "api_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_image_generation_tasks_created_at",
                table: "image_generation_tasks",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_image_generation_tasks_status",
                table: "image_generation_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_image_generation_tasks_user_id",
                table: "image_generation_tasks",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_tokens");

            migrationBuilder.DropTable(
                name: "image_generation_tasks");
        }
    }
}
