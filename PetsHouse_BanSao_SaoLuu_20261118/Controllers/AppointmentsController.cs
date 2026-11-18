using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
using PetsHouse.Services;
namespace PetsHouse.Controllers;
[Authorize(Roles=Roles.Customer)]
public class AppointmentsController(AppDbContext db,UserManager<ApplicationUser> users,BookingService booking):Controller
{
    private Task<int> CustomerId()=>db.Customers.Where(x=>x.UserId==users.GetUserId(User)).Select(x=>x.Id).SingleAsync();
    public async Task<IActionResult> Index()
    {
        int id=await CustomerId();
        return View(await db.Appointments.Include(x=>x.Pet).Include(x=>x.ClinicService).Include(x=>x.Veterinarian).Where(x=>x.CustomerId==id).OrderByDescending(x=>x.StartsAt).ToListAsync());
    }
    [HttpGet]public async Task<IActionResult> Create()
    {
        await Populate();return View(new AppointmentVm{StartsAt=DateTime.Now.AddDays(1).Date.AddHours(9)});
    }
    private async Task Populate()
    {
        var id=await CustomerId();
        ViewBag.Pets=await db.Pets.Where(x=>x.CustomerId==id).OrderBy(x=>x.Name).ToListAsync();
        ViewBag.Services=await db.ClinicServices.Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync();
        ViewBag.Doctors=await db.Veterinarians.Where(x=>x.IsActive).OrderBy(x=>x.FullName).ToListAsync();
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentVm vm)
    {
        if(ModelState.IsValid)
        {
            var result=await booking.BookAsync(await CustomerId(),vm);
            if(result.Ok){TempData["Success"]="Đã gửi yêu cầu đặt lịch.";return RedirectToAction(nameof(Index));}
            ModelState.AddModelError("",result.Error);
        }
        await Populate();return View(vm);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var cid=await CustomerId();
        var x=await db.Appointments.FirstOrDefaultAsync(x=>x.Id==id&&x.CustomerId==cid);
        if(x is null)return NotFound();
        if(x.Status==AppointmentStatus.Canceled||x.Status==AppointmentStatus.Completed||x.StartsAt<DateTime.Now.AddDays(1))
            TempData["Error"]="Không thể hủy: lịch đã hoàn thành, đã hủy hoặc còn dưới 24 giờ.";
        else{x.Status=AppointmentStatus.Canceled;await db.SaveChangesAsync();TempData["Success"]="Đã hủy lịch.";}
        return RedirectToAction(nameof(Index));
    }
}
