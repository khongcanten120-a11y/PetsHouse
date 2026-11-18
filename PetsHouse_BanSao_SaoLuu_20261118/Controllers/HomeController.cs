using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Controllers;

public class HomeController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var services = await db.ClinicServices
            .AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .Take(8)
            .ToListAsync();

        ViewBag.Doctors = await db.Veterinarians
            .AsNoTracking()
            .Where(doctor => doctor.IsActive)
            .OrderBy(doctor => doctor.FullName)
            .ToListAsync();

        ViewBag.Pets = new List<Pet>();

        if (User.IsInRole(Roles.Customer))
        {
            var userId = userManager.GetUserId(User);
            var customerId = await db.Customers
                .Where(customer => customer.UserId == userId)
                .Select(customer => (int?)customer.Id)
                .FirstOrDefaultAsync();

            if (customerId.HasValue)
            {
                ViewBag.Pets = await db.Pets
                    .AsNoTracking()
                    .Where(pet => pet.CustomerId == customerId.Value)
                    .OrderBy(pet => pet.Name)
                    .ToListAsync();
            }
        }

        return View(services);
    }

    public IActionResult Error()
    {
        return View();
    }
}
