using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdAndDeviceTypeToUserSessionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "UserSessionLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "UserSessionLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionLogs_UserId_SessionId_IsActive",
                table: "UserSessionLogs",
                columns: new[] { "UserId", "SessionId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessionLogs_UserId_SessionId_IsActive",
                table: "UserSessionLogs");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "UserSessionLogs");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "UserSessionLogs");
        }
    }
}
