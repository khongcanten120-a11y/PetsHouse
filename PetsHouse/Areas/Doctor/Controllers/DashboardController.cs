using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = Roles.Doctor)]
public class DashboardController(
    AppDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var doctor = await db.Veterinarians.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == users.GetUserId(User));

        var vm = new DoctorQueueVm();

        if (doctor is null)
        {
            return View(vm);
        }

        vm.DoctorName = doctor.FullName;

        // Chỉ ca Waiting được hiển thị ở hàng chờ và có nút Khám bệnh.
        vm.WaitingAppointments = await db.Appointments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Where(x => x.VeterinarianId == doctor.Id &&
                        x.Status == AppointmentStatus.Waiting)
            .OrderBy(x => x.StartsAt)
            .ThenBy(x => x.Id)
            .ToListAsync();

        // Giữ các ca khác để bác sĩ tra cứu; không đẩy lịch đã hoàn thành lên trên.
        vm.OtherAppointments = await db.Appointments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Where(x => x.VeterinarianId == doctor.Id &&
                        x.Status != AppointmentStatus.Waiting)
            .OrderByDescending(x => x.StartsAt)
            .ThenByDescending(x => x.Id)
            .Take(50)
            .ToListAsync();

        return View(vm);
    }
}
