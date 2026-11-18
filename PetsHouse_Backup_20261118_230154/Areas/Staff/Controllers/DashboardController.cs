using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Areas.Staff.Controllers;
[Area("Staff"),Authorize(Roles=Roles.Staff)]
public class DashboardController(AppDbContext db):Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Doctors=await db.Veterinarians.Where(x=>x.IsActive).ToListAsync();
        return View(await db.Appointments.Include(x=>x.Pet).Include(x=>x.Customer).Include(x=>x.ClinicService).Include(x=>x.Veterinarian).OrderBy(x=>x.StartsAt).Take(100).ToListAsync());
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id,int doctorId)
    {
        var appt=await db.Appointments.Include(x=>x.ClinicService).FirstOrDefaultAsync(x=>x.Id==id);
        if(appt is null)return NotFound();
        if(appt.Status!=AppointmentStatus.Pending&&appt.Status!=AppointmentStatus.Confirmed){TempData["Error"]="Trạng thái lịch không hợp lệ.";return RedirectToAction(nameof(Index));}
        var doctor=await db.Veterinarians.FirstOrDefaultAsync(x=>x.Id==doctorId&&x.IsActive);
        if(doctor is null){TempData["Error"]="Bác sĩ không tồn tại.";return RedirectToAction(nameof(Index));}
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var shift=await db.WorkShifts.AnyAsync(x=>x.VeterinarianId==doctorId&&x.StartAt<=appt.StartsAt&&x.EndAt>=appt.EndsAt);
        var busy=await db.Appointments.AnyAsync(x=>x.Id!=appt.Id&&x.VeterinarianId==doctorId&&x.Status!=AppointmentStatus.Canceled&&x.Status!=AppointmentStatus.Completed&&x.StartsAt<appt.EndsAt&&x.EndsAt>appt.StartsAt);
        if(!shift||busy){TempData["Error"]="Bác sĩ không có ca trống phù hợp.";return RedirectToAction(nameof(Index));}
        appt.VeterinarianId=doctorId;appt.Status=AppointmentStatus.Waiting;
        await db.SaveChangesAsync();await tx.CommitAsync();TempData["Success"]="Đã xác nhận và chuyển chờ khám.";return RedirectToAction(nameof(Index));
    }
}
