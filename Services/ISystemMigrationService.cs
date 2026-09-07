using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class MigrationEntitySummary
    {
        public string EntityName { get; set; } = string.Empty;
        public int TotalFound { get; set; }
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class SystemMigrationResult
    {
        public bool IsSuccess => EntitySummaries.All(e => e.Errors.Count == 0);
        public int TotalImported => EntitySummaries.Sum(e => e.ImportedCount);
        public int TotalSkipped => EntitySummaries.Sum(e => e.SkippedCount);
        public List<MigrationEntitySummary> EntitySummaries { get; set; } = new List<MigrationEntitySummary>();
        public List<string> GeneralMessages { get; set; } = new List<string>();
    }

    public interface ISystemMigrationService
    {
        /// <summary>
        /// Generates a fully formatted, multi-sheet Excel (.xlsx) workbook template for legacy data migration
        /// covering Departments, Doctors, Patients, Appointments, Treatments, and Invoices.
        /// </summary>
        byte[] GenerateFullMigrationExcelTemplate();

        /// <summary>
        /// Parses a multi-sheet Excel (.xlsx) workbook and orchestrates migration across all clinical entities
        /// with automated foreign key resolution and relational linking.
        /// </summary>
        Task<SystemMigrationResult> MigrateFromExcelAsync(Stream excelStream);

        /// <summary>
        /// Exports all database tables across all entities into a single multi-sheet Excel (.xlsx) backup file.
        /// </summary>
        Task<byte[]> ExportFullSystemBackupExcelAsync();
    }
}
