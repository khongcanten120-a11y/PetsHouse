using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Areas.Doctor.Controllers;
[Area("Doctor"),Authorize(Roles=Roles.Doctor)]
public class DashboardController(AppDbContext db,UserManager<ApplicationUser> users):Controller
{
    public async Task<IActionResult> Index()
    {
        var doctor=await db.Veterinarians.FirstOrDefaultAsync(x=>x.UserId==users.GetUserId(User));
        if(doctor is null)return View(new List<Appointment>());
        return View(await db.Appointments.Include(x=>x.Pet).Include(x=>x.Customer).Include(x=>x.MedicalRecord).Where(x=>x.VeterinarianId==doctor.Id).OrderByDescending(x=>x.StartsAt).Take(100).ToListAsync());
    }
}
