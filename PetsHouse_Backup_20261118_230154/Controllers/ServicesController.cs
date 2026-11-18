using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
namespace PetsHouse.Controllers;
public class ServicesController(AppDbContext db):Controller
{
    public async Task<IActionResult> Index() => View(await db.ClinicServices.Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync());
}
