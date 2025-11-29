using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verdure.Mcp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServiceAndTokenUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "daily_image_limit",
                table: "api_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_image_count_reset",
                table: "api_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "today_image_count",
                table: "api_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "user_id",
                table: "api_tokens",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mcp_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    endpoint_route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_free = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    documentation_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_services", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_user_id",
                table: "api_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_services_category",
                table: "mcp_services",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_services_is_enabled",
                table: "mcp_services",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_services_name",
                table: "mcp_services",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_services");

            migrationBuilder.DropIndex(
                name: "IX_api_tokens_user_id",
                table: "api_tokens");

            migrationBuilder.DropColumn(
                name: "daily_image_limit",
                table: "api_tokens");

            migrationBuilder.DropColumn(
                name: "last_image_count_reset",
                table: "api_tokens");

            migrationBuilder.DropColumn(
                name: "today_image_count",
                table: "api_tokens");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "api_tokens");
        }
    }
}
