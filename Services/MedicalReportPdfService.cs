using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Generates a branded PDF medical summary report for a single patient
    /// using QuestPDF's fluent API.
    /// </summary>
    public static class MedicalReportPdfService
    {
        // ── Clinic branding colours ───────────────────────────────────────────
        private static readonly string AccentHex    = "#2563eb";
        private static readonly string LightBg      = "#f0f7ff";
        private static readonly string MutedText    = "#64748b";
        private static readonly string BorderColor  = "#e2e8f0";
        private static readonly string DangerColor  = "#dc2626";

        public static byte[] Generate(
            Patient patient,
            List<MedicalRecord>      medicalRecords,
            List<Appointment>        appointments,
            List<Treatment>          treatments,
            List<PatientAttachment>  attachments,
            string clinicName = "MediCare Clinic",
            string clinicPhone = "",
            string clinicAddress = "")
        {
            // QuestPDF Community licence (free for open-source / small revenue)
            QuestPDF.Settings.License = LicenseType.Community;

            using var ms = new MemoryStream();
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // ── Header ────────────────────────────────────────────────
                    page.Header().Element(ComposeHeader);

                    // ── Content ───────────────────────────────────────────────
                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        // Clinic banner
                        col.Item().Element(c => ComposeBanner(c, clinicName, clinicPhone, clinicAddress));

                        // Patient demographics
                        col.Item().Element(c => ComposeDemographics(c, patient));

                        // Medical records timeline
                        if (medicalRecords.Any())
                            col.Item().Element(c => ComposeMedicalTimeline(c, medicalRecords));

                        // Treatment history from appointments
                        if (treatments.Any())
                            col.Item().Element(c => ComposeTreatments(c, treatments, appointments));

                        // Attachments list
                        if (attachments.Any())
                            col.Item().Element(c => ComposeAttachments(c, attachments));
                    });

                    // ── Footer ────────────────────────────────────────────────
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(ms);

            return ms.ToArray();
        }

        // ── Header: watermark-style ───────────────────────────────────────────
        private static void ComposeHeader(IContainer c)
        {
            c.Row(row =>
            {
                row.RelativeItem().Text("CONFIDENTIAL — MEDICAL RECORD")
                   .FontSize(7).FontColor(MutedText).Italic();
                row.ConstantItem(160).AlignRight()
                   .Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                   .FontSize(7).FontColor(MutedText);
            });
        }

        // ── Clinic banner ─────────────────────────────────────────────────────
        private static void ComposeBanner(IContainer c,
            string name, string phone, string address)
        {
            c.Background(AccentHex).Padding(16).Row(row =>
            {
                // Icon placeholder
                row.ConstantItem(48).AlignMiddle()
                   .Width(40).Height(40)
                   .Background("#ffffff20")
                   .AlignCenter().AlignMiddle()
                   .Text("🏥").FontSize(22);

                row.ConstantItem(12); // spacer

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(name)
                       .FontSize(18).Bold().FontColor("#ffffff");
                    if (!string.IsNullOrWhiteSpace(phone))
                        col.Item().Text($"☎ {phone}  •  {address}")
                           .FontSize(8).FontColor("#ffffffcc");
                });

                row.ConstantItem(120).AlignRight().AlignMiddle()
                   .Text("Medical Report")
                   .FontSize(11).Bold().FontColor("#ffffff80");
            });
        }

        // ── Patient demographics ──────────────────────────────────────────────
        private static void ComposeDemographics(IContainer c, Patient patient)
        {
            int age = DateTime.Today.Year - patient.DOB.Year;
            if (patient.DOB.Date > DateTime.Today.AddYears(-age)) age--;

            c.Border(1).BorderColor(BorderColor).Padding(12).Column(col =>
            {
                col.Item().Background(LightBg).Padding(6)
                   .Text("Patient Profile").FontSize(11).Bold().FontColor(AccentHex);

                col.Item().PaddingTop(8).Row(row =>
                {
                    // Left column
                    row.RelativeItem().Column(left =>
                    {
                        FieldRow(left, "Full Name",    patient.PatientName);
                        FieldRow(left, "Patient ID",   patient.PatientNumber);
                        FieldRow(left, "National ID",  patient.NationalId);
                        FieldRow(left, "Phone",        patient.PhoneNumber);
                    });
                    // Right column
                    row.RelativeItem().Column(right =>
                    {
                        FieldRow(right, "Date of Birth", patient.DOB.ToString("dd MMM yyyy"));
                        FieldRow(right, "Age",           $"{age} years");
                        FieldRow(right, "Blood Type",    patient.BloodType ?? "N/A");
                        FieldRow(right, "Allergies",     patient.Allergies ?? "None recorded");
                    });
                });

                if (!string.IsNullOrWhiteSpace(patient.ChronicDiseases))
                {
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.ConstantItem(100).Text("Chronic Diseases").FontColor(MutedText).FontSize(9);
                        r.RelativeItem().Text(patient.ChronicDiseases).FontColor(DangerColor).FontSize(9);
                    });
                }
            });
        }

        // ── Medical timeline ──────────────────────────────────────────────────
        private static void ComposeMedicalTimeline(IContainer c, List<MedicalRecord> records)
        {
            c.Column(col =>
            {
                col.Item().Background(LightBg).Padding(6)
                   .Text("Medical History & Visits").FontSize(11).Bold().FontColor(AccentHex);

                foreach (var rec in records.OrderByDescending(r => r.VisitDate))
                {
                    col.Item().PaddingTop(6).Border(1).BorderColor(BorderColor).Padding(10).Column(entry =>
                    {
                        entry.Item().Row(r =>
                        {
                            r.RelativeItem().Text(rec.VisitDate.ToString("dd MMM yyyy"))
                             .Bold().FontSize(10).FontColor(AccentHex);
                            if (rec.Doctor != null)
                                r.ConstantItem(180).AlignRight()
                                 .Text($"Dr. {rec.Doctor.DoctorName}")
                                 .FontSize(9).FontColor(MutedText);
                        });

                        entry.Item().PaddingTop(3)
                             .Text($"Chief Complaint: {rec.ChiefComplaint}")
                             .FontSize(9).Italic();

                        if (!string.IsNullOrWhiteSpace(rec.Diagnosis))
                            entry.Item().PaddingTop(2)
                                 .Text($"Diagnosis: {rec.Diagnosis}")
                                 .FontSize(9);

                        if (!string.IsNullOrWhiteSpace(rec.TreatmentPlan))
                            entry.Item().PaddingTop(2)
                                 .Text($"Treatment Plan: {rec.TreatmentPlan}")
                                 .FontSize(9).FontColor("#0f5132");

                        if (!string.IsNullOrWhiteSpace(rec.Notes))
                            entry.Item().PaddingTop(2)
                                 .Text($"Notes: {rec.Notes}")
                                 .FontSize(8).FontColor(MutedText).Italic();
                    });
                }
            });
        }

        // ── Treatment history ─────────────────────────────────────────────────
        private static void ComposeTreatments(IContainer c,
            List<Treatment> treatments, List<Appointment> appointments)
        {
            c.Column(col =>
            {
                col.Item().Background(LightBg).Padding(6)
                   .Text("Treatment History").FontSize(11).Bold().FontColor(AccentHex);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(80);   // date
                        cols.RelativeColumn(2);    // description
                        cols.RelativeColumn(2);    // diagnosis
                        cols.ConstantColumn(70);   // cost
                    });

                    // Header row
                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Date", "Treatment", "Diagnosis", "Cost" })
                            h.Cell().Background(AccentHex).Padding(4)
                             .Text(hdr).FontColor("#ffffff").Bold().FontSize(9);
                    });

                    foreach (var t in treatments.OrderByDescending(x => x.TreatmentDate))
                    {
                        var bg = treatments.IndexOf(t) % 2 == 0 ? "#ffffff" : "#f8fafc";
                        Cell(table, t.TreatmentDate.ToString("dd MMM yy"), bg);
                        Cell(table, t.TreatmentDesc, bg);
                        Cell(table, t.Diagnosis ?? "—", bg);
                        Cell(table, $"${t.TreatmentCost:N0}", bg);
                    }
                });
            });
        }

        // ── Attachments list ──────────────────────────────────────────────────
        private static void ComposeAttachments(IContainer c, List<PatientAttachment> attachments)
        {
            c.Column(col =>
            {
                col.Item().Background(LightBg).Padding(6)
                   .Text("Documents & Attachments").FontSize(11).Bold().FontColor(AccentHex);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(80);
                        cols.ConstantColumn(80);
                        cols.ConstantColumn(100);
                    });

                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "File Name", "Type", "Category", "Uploaded" })
                            h.Cell().Background("#475569").Padding(4)
                             .Text(hdr).FontColor("#ffffff").Bold().FontSize(9);
                    });

                    foreach (var att in attachments.OrderByDescending(a => a.UploadedAt))
                    {
                        var bg = attachments.IndexOf(att) % 2 == 0 ? "#ffffff" : "#f8fafc";
                        Cell(table, att.FileName, bg);
                        Cell(table, att.FileType.Replace("image/", "").Replace("application/", "").ToUpper(), bg);
                        Cell(table, att.Category, bg);
                        Cell(table, att.UploadedAt.ToLocalTime().ToString("dd MMM yy"), bg);
                    }
                });
            });
        }

        // ── Footer ───────────────────────────────────────────────────────────
        private static void ComposeFooter(IContainer c)
        {
            c.BorderTop(1).BorderColor(BorderColor).PaddingTop(6).Row(row =>
            {
                row.RelativeItem()
                   .Text("This document is confidential and intended solely for authorised medical personnel.")
                   .FontSize(7).FontColor(MutedText).Italic();
                row.ConstantItem(60).AlignRight()
                   .Text(x =>
                   {
                       x.Span("Page ").FontSize(7).FontColor(MutedText);
                       x.CurrentPageNumber().FontSize(7).FontColor(MutedText);
                       x.Span(" of ").FontSize(7).FontColor(MutedText);
                       x.TotalPages().FontSize(7).FontColor(MutedText);
                   });
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void FieldRow(ColumnDescriptor col, string label, string value)
        {
            col.Item().PaddingBottom(4).Row(r =>
            {
                r.ConstantItem(90).Text(label).FontSize(9).FontColor(MutedText);
                r.RelativeItem().Text(value).FontSize(9).Bold();
            });
        }

        private static void Cell(TableDescriptor t, string text, string bg)
        {
            t.Cell().Background(bg).Padding(4).Text(text).FontSize(9);
        }
    }
}
