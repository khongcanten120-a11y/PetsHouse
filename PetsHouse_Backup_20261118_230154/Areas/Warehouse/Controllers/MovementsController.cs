using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Warehouse.Controllers;

[Area("Warehouse"), Authorize(Roles = Roles.Warehouse)]
public class MovementsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(await db.StockMovements.AsNoTracking().Include(x => x.Medicine)
            .OrderByDescending(x => x.CreatedAtUtc).Take(150).ToListAsync());
    }
}
