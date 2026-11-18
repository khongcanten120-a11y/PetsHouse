using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class CustomersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 15;
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => x.Name.Contains(q) || x.Phone.Contains(q) || x.User.Email!.Contains(q));
        }
        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.Total = await query.CountAsync();
        ViewBag.PageSize = pageSize;
        return View(await query.Include(x => x.User).Include(x => x.Pets)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await db.Customers.AsNoTracking().Include(x => x.User)
            .Include(x => x.Pets).FirstOrDefaultAsync(x => x.Id == id);
        if (customer is null) return NotFound();
        ViewBag.Appointments = await db.Appointments.AsNoTracking()
            .Include(x => x.Pet).Include(x => x.ClinicService)
            .Where(x => x.CustomerId == id).OrderByDescending(x => x.StartsAt).Take(30).ToListAsync();
        return View(customer);
    }
}
