using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class ImportResult<T>
    {
        public int TotalRows { get; set; }
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public List<T> ImportedRecords { get; set; } = new List<T>();
    }

    public interface IDataImportExportService
    {
        // ── Patient Import / Export ───────────────────────────────────────────
        byte[] GeneratePatientTemplateCsv();
        Task<byte[]> ExportPatientsToCsvAsync();
        Task<ImportResult<Patient>> ImportPatientsFromCsvAsync(Stream stream);

        // ── Doctor Import / Export ────────────────────────────────────────────
        byte[] GenerateDoctorTemplateCsv();
        Task<byte[]> ExportDoctorsToCsvAsync();
        Task<ImportResult<Doctor>> ImportDoctorsFromCsvAsync(Stream stream);

        // ── Appointment Import / Export ───────────────────────────────────────
        byte[] GenerateAppointmentTemplateCsv();
        Task<byte[]> ExportAppointmentsToCsvAsync();
        Task<ImportResult<Appointment>> ImportAppointmentsFromCsvAsync(Stream stream);

        // ── Invoice Import / Export ───────────────────────────────────────────
        byte[] GenerateInvoiceTemplateCsv();
        Task<byte[]> ExportInvoicesToCsvAsync();
        Task<ImportResult<Invoice>> ImportInvoicesFromCsvAsync(Stream stream);
    }
}
