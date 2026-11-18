using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
using PetsHouse.Services;

namespace PetsHouse.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = Roles.Staff)]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;

        var vm = new AdminAppointmentListVm
        {
            Page = page,
            PageSize = pageSize,
            Total = await db.Appointments.CountAsync()
        };

        vm.Appointments = await db.Appointments
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Include(x => x.Veterinarian)
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Staff và Admin sử dụng đúng cùng 1 bộ lọc và cùng 1 nguồn dữ liệu.
        await AppointmentConfirmationService.FillPendingAsync(db, vm);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> PendingSnapshot()
    {
        return Json(await AppointmentConfirmationService.ReadPendingSnapshotAsync(db));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, int doctorId)
    {
        var result = await AppointmentConfirmationService
            .ConfirmAsync(db, id, doctorId);

        TempData[result.Ok ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
