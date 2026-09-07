using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class EmailService
    {
        public async Task SendAppointmentReminder(string patientEmail, string patientName, string appointmentDate)
        {
            // ملاحظة: لاستخدام Gmail، يجب تفعيل "App Password" من إعدادات حساب جوجل
            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential("your-email@gmail.com", "your-app-password"),
                EnableSsl = true
            };

            var mail = new MailMessage("clinic@system.com", patientEmail, "Appointment Reminder",
                $"Dear {patientName}, you have an appointment on {appointmentDate}. See you soon!");

            await client.SendMailAsync(mail);
        }
    }
}