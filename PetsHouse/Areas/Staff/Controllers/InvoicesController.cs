using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = Roles.Staff)]
public class InvoicesController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;
        var query = db.Invoices.AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Paid);

        ViewBag.Total = await query.CountAsync();
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;

        return View(await query
            .Include(x => x.Appointment).ThenInclude(x => x.Customer)
            .Include(x => x.Appointment).ThenInclude(x => x.Pet)
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync());
    }
}
