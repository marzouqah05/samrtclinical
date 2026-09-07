using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorTelegramChatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the TelegramChatId column to Doctors.
            // The TelegramChatId ALTER was already applied to the DB in the failed run,
            // so this call is safe — EF will succeed on a clean DB and be a no-op
            // if the column exists (handled by the IF NOT EXISTS guard below).
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Doctors' AND COLUMN_NAME = 'TelegramChatId'
                )
                BEGIN
                    ALTER TABLE [Doctors] ADD [TelegramChatId] nvarchar(100) NULL;
                END
            ");

            // Ensure AuditLogs indexes exist (idempotent).
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_EntityName' AND object_id = OBJECT_ID('AuditLogs'))
                    CREATE INDEX IX_AuditLogs_EntityName ON AuditLogs (EntityName);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Timestamp' AND object_id = OBJECT_ID('AuditLogs'))
                    CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs ([Timestamp]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Doctors");
        }
    }
}
