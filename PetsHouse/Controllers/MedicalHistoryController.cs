using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Controllers;

[Authorize(Roles = Roles.Customer)]
public class MedicalHistoryController(AppDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index(int? petId)
    {
        var uid = users.GetUserId(User);
        var customerId = await db.Customers.Where(x => x.UserId == uid)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (customerId is null) return NotFound();
        ViewBag.Pets = await db.Pets.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Name).ToListAsync();
        ViewBag.PetId = petId;
        var query = db.Appointments.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.MedicalRecord != null);
        if (petId.HasValue) query = query.Where(x => x.PetId == petId.Value);
        return View(await query.Include(x => x.Pet).Include(x => x.ClinicService)
            .Include(x => x.Veterinarian).Include(x => x.MedicalRecord)!
            .ThenInclude(x => x.Prescriptions).ThenInclude(x => x.Medicine)
            .OrderByDescending(x => x.StartsAt).Take(100).ToListAsync());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var uid = users.GetUserId(User);
        var appointment = await db.Appointments.AsNoTracking()
            .Where(x => x.Id == id && x.Customer.UserId == uid && x.MedicalRecord != null)
            .Include(x => x.Pet).Include(x => x.ClinicService).Include(x => x.Veterinarian)
            .Include(x => x.MedicalRecord)!.ThenInclude(x => x.Prescriptions).ThenInclude(x => x.Medicine)
            .Include(x => x.Invoice)!.ThenInclude(x => x.Items)
            .FirstOrDefaultAsync();
        return appointment is null ? NotFound() : View(appointment);
    }
}
