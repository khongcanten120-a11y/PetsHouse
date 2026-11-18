using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class MedicalRecordsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = await db.MedicalRecords.CountAsync();

        return View(await db.MedicalRecords
            .AsNoTracking()
            .Include(record => record.Appointment)
                .ThenInclude(appointment => appointment.Pet)
            .Include(record => record.Appointment)
                .ThenInclude(appointment => appointment.Customer)
            .OrderByDescending(record => record.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var record = await db.MedicalRecords
            .AsNoTracking()
            .Include(record => record.Appointment)
                .ThenInclude(appointment => appointment.Pet)
            .Include(record => record.Prescriptions)
                .ThenInclude(prescription => prescription.Medicine)
            .FirstOrDefaultAsync(record => record.Id == id);

        return record is null ? NotFound() : View(record);
    }
}
