using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class BackupFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BackupType { get; set; } = "Daily Snapshot"; // Daily Snapshot | Monthly Archive
        public string FormattedSize
        {
            get
            {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                return $"{SizeBytes / (1024.0 * 1024.0):F2} MB";
            }
        }
    }

    public class CloudSyncStatus
    {
        public bool IsConnected { get; set; } = true;
        public string ProviderName { get; set; } = "Google Drive (Encrypted Cloud Vault)";
        public DateTime? LastSyncTime { get; set; }
        public string StatusMessage { get; set; } = "Connected & Synchronized";
        public bool IsSuccess { get; set; } = true;
        public string LastSyncedFile { get; set; } = string.Empty;
    }

    public interface IBackupService
    {
        /// <summary>
        /// Generates a live daily snapshot ZIP containing a complete JSON database dump.
        /// </summary>
        Task<byte[]> GenerateDailySnapshotAsync();

        /// <summary>
        /// Generates a full system monthly archive ZIP containing database dump + all patient attachments.
        /// </summary>
        Task<byte[]> GenerateFullSystemArchiveAsync();

        /// <summary>
        /// Synchronizes the latest system snapshot to Google Drive / Cloud storage.
        /// </summary>
        Task<CloudSyncStatus> SyncToGoogleDriveAsync();

        /// <summary>
        /// Gets list of recent local backup archives.
        /// </summary>
        List<BackupFileInfo> GetRecentBackups();

        /// <summary>
        /// Checks if the monthly offline backup reminder should be displayed (e.g. on 1st of month).
        /// </summary>
        bool IsMonthlyReminderActive();

        /// <summary>
        /// Gets current cloud sync status.
        /// </summary>
        Task<CloudSyncStatus> GetCloudSyncStatusAsync();
    }
}
