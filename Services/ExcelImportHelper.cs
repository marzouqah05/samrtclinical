using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WebApplication1.Services
{
    public static class ExcelImportHelper
    {
        public static bool IsSupportedFile(IFormFile? file)
        {
            if (file == null || file.Length == 0) return false;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            return ext == ".csv" || ext == ".xlsx" || ext == ".xls";
        }

        public static Stream GetStreamAsCsv(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext == ".csv")
            {
                return file.OpenReadStream();
            }

            // Convert Excel (ClosedXML) first sheet into an in-memory CSV stream with UTF-8 BOM
            using var excelStream = file.OpenReadStream();
            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                return new MemoryStream();
            }

            var csvStream = new MemoryStream();
            using (var writer = new StreamWriter(csvStream, new UTF8Encoding(true), 1024, leaveOpen: true))
            {
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                for (int r = 1; r <= lastRow; r++)
                {
                    var rowValues = new List<string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        var cell = worksheet.Cell(r, c);
                        string val = cell.GetString()?.Trim() ?? "";
                        if (val.Contains("\"") || val.Contains(",") || val.Contains("\n") || val.Contains("\r"))
                        {
                            val = $"\"{val.Replace("\"", "\"\"")}\"";
                        }
                        rowValues.Add(val);
                    }
                    writer.WriteLine(string.Join(",", rowValues));
                }
                writer.Flush();
            }

            csvStream.Position = 0;
            return csvStream;
        }
    }
}
