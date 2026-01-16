using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verdure.Mcp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAndSignalRSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mac_address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    target_user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_bindings", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_bindings_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_connections",
                columns: table => new
                {
                    connection_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_connections", x => x.connection_id);
                    table.ForeignKey(
                        name: "FK_device_connections_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_bindings_device_id",
                table: "device_bindings",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_bindings_device_id_target_user_id",
                table: "device_bindings",
                columns: new[] { "device_id", "target_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_bindings_owner_user_id",
                table: "device_bindings",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_bindings_target_user_id",
                table: "device_bindings",
                column: "target_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_connections_device_id",
                table: "device_connections",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_connections_user_id",
                table: "device_connections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_devices_mac_address",
                table: "devices",
                column: "mac_address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_owner_user_id",
                table: "devices",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_devices_status",
                table: "devices",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_bindings");

            migrationBuilder.DropTable(
                name: "device_connections");

            migrationBuilder.DropTable(
                name: "devices");
        }
    }
}
