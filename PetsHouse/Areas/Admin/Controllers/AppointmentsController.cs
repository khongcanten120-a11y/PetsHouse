using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
using PetsHouse.Services;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class AppointmentsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(
        AppointmentStatus? status,
        DateTime? day,
        string? q,
        int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;

        q = q?.Trim();
        if (q?.Length > 80)
        {
            q = q[..80];
        }

        var query = db.Appointments.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (day.HasValue)
        {
            var from = day.Value.Date;
            var to = from.AddDays(1);
            query = query.Where(x => x.StartsAt >= from && x.StartsAt < to);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Customer.Name.Contains(q) || x.Pet.Name.Contains(q));
        }

        var vm = new AdminAppointmentListVm
        {
            Status = status,
            Day = day,
            Query = q ?? "",
            Page = page,
            PageSize = pageSize,
            Total = await query.CountAsync()
        };

        vm.Appointments = await query
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Include(x => x.Veterinarian)
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Cùng hàng chờ với Staff, luôn nằm trên danh sách chính.
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
