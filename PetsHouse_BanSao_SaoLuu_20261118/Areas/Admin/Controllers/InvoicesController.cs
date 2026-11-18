using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class InvoicesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;

        ViewBag.Total = await db.Invoices.CountAsync();
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;

        return View(await db.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Appointment)
                .ThenInclude(appointment => appointment.Customer)
            .Include(invoice => invoice.Appointment)
                .ThenInclude(appointment => appointment.Pet)
            .OrderByDescending(invoice => invoice.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Appointment)
                .ThenInclude(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == id);

        return invoice is null ? NotFound() : View(invoice);
    }
}
