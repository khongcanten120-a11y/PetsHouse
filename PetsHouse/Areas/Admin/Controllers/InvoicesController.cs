
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
    // Danh sách hóa đơn
    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);

        const int pageSize = 20;

        // Lấy tổng số hóa đơn
        ViewBag.Total = await db.Invoices.CountAsync();

        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;

        // Lấy hóa đơn theo ID tăng dần
        var invoices = await db.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Appointment)
                .ThenInclude(appointment => appointment.Customer)
            .Include(invoice => invoice.Appointment)
                .ThenInclude(appointment => appointment.Pet)
            .OrderBy(invoice => invoice.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(invoices);
    }

    // Xem chi tiết hóa đơn
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Appointment)
                .ThenInclude(appointment => appointment.Customer)
            .Include(invoice => invoice.Appointment)
                .ThenInclude(appointment => appointment.Pet)
            .FirstOrDefaultAsync(invoice => invoice.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(invoice);
    }
}
