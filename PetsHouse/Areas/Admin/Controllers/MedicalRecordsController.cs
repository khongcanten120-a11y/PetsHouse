
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

        var query = db.MedicalRecords
            .AsNoTracking();

        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = await query.CountAsync();

        // Sắp xếp bệnh án theo ID tăng dần
        var records = await query
            .Include(record => record.Appointment)
                .ThenInclude(appointment => appointment.Pet)
            .Include(record => record.Appointment)
                .ThenInclude(appointment => appointment.Customer)
            .OrderBy(record => record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(records);
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

        if (record is null)
        {
            return NotFound();
        }

        return View(record);
    }
}
