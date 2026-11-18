using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class MedicinesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Medicines.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length > 80)
            {
                q = q[..80];
            }
            query = query.Where(medicine => medicine.Name.Contains(q));
        }

        ViewBag.Query = q;
        return View(await query.OrderBy(medicine => medicine.Name).Take(300).ToListAsync());
    }
}
