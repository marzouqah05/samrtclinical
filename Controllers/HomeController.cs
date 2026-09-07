using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ClinicDbContext _context;

        public HomeController(ClinicDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.DepartmentsCount = _context.Departments.Count();
            ViewBag.DoctorsCount = _context.Doctors.Count();
            ViewBag.PatientsCount = _context.Patients.Count();
            ViewBag.AppointmentsCount = _context.Appointments.Count();

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}