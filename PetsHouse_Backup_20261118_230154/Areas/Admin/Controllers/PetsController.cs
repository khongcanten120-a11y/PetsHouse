using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class PetsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;
        var pets = db.Pets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length > 80)
            {
                q = q[..80];
            }
            pets = pets.Where(pet =>
                pet.Name.Contains(q) ||
                pet.Species.Contains(q) ||
                pet.Customer.Name.Contains(q));
        }

        ViewBag.Query = q;
        ViewBag.Total = await pets.CountAsync();
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;

        return View(await pets
            .Include(pet => pet.Customer)
            .OrderBy(pet => pet.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync());
    }
}
