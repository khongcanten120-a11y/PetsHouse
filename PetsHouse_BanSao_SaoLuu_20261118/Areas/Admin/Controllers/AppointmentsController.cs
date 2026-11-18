using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

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

        var query = db.Appointments.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(appointment => appointment.Status == status.Value);
        }

        if (day.HasValue)
        {
            var from = day.Value.Date;
            var to = from.AddDays(1);
            query = query.Where(appointment =>
                appointment.StartsAt >= from && appointment.StartsAt < to);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length > 80)
            {
                q = q[..80];
            }

            query = query.Where(appointment =>
                appointment.Customer.Name.Contains(q) ||
                appointment.Pet.Name.Contains(q));
        }

        ViewBag.Status = status;
        ViewBag.Day = day;
        ViewBag.Query = q;
        ViewBag.CurrentPage = page;
        ViewBag.Total = await query.CountAsync();
        ViewBag.PageSize = pageSize;

        var appointments = await query
            .Include(appointment => appointment.Pet)
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.ClinicService)
            .Include(appointment => appointment.Veterinarian)
            .OrderByDescending(appointment => appointment.StartsAt)
            .ThenByDescending(appointment => appointment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(appointments);
    }
}
