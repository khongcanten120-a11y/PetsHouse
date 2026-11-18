using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = Roles.Doctor)]
public class WorkShiftsController(
    AppDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    private Task<Veterinarian?> CurrentDoctorAsync()
    {
        var userId = users.GetUserId(User);
        return db.Veterinarians.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.IsActive);
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var now = DateTime.Today;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        if (y < 2020 || y > 2100 || m < 1 || m > 12)
        {
            return RedirectToAction(nameof(Index), new
            {
                year = now.Year,
                month = now.Month
            });
        }

        var doctor = await CurrentDoctorAsync();
        var gridStart = ShiftCalendarFactory.GetGridStart(y, m);
        var gridEnd = gridStart.AddDays(42);

        var shifts = doctor is null
            ? new List<WorkShift>()
            : await db.WorkShifts
                .AsNoTracking()
                .Include(x => x.Veterinarian)
                .Where(x => x.VeterinarianId == doctor.Id &&
                            x.StartAt < gridEnd && x.EndAt > gridStart)
                .OrderBy(x => x.StartAt)
                .ToListAsync();

        var vm = ShiftCalendarFactory.Build(y, m, shifts,
            doctor is null ? new List<Veterinarian>() : new List<Veterinarian> { doctor });

        vm.HasDoctorProfile = doctor is not null;
        vm.CurrentDoctorName = doctor?.FullName;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DoctorShiftForm vm)
    {
        var doctor = await CurrentDoctorAsync();
        if (doctor is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid ||
            !ShiftCatalog.TryGetWindow(vm.WorkDate, vm.ShiftCode, out var start, out var end))
        {
            TempData["Error"] = "Ngày hoặc ca làm việc không hợp lệ.";
            return Back(vm.WorkDate);
        }

        var validationError = ShiftCatalog.ValidateFuture(start, end);
        if (validationError is not null)
        {
            TempData["Error"] = validationError;
            return Back(vm.WorkDate);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var overlapping = await db.WorkShifts.AnyAsync(x =>
            x.VeterinarianId == doctor.Id &&
            x.StartAt < end && x.EndAt > start);

        if (overlapping)
        {
            TempData["Error"] = "Bạn đã có ca làm trùng với khung giờ này.";
            return Back(vm.WorkDate);
        }

        db.WorkShifts.Add(new WorkShift
        {
            VeterinarianId = doctor.Id,
            StartAt = start,
            EndAt = end
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Đã đăng ký ca làm việc thành công.";
        return Back(vm.WorkDate);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var doctor = await CurrentDoctorAsync();
        if (doctor is null)
        {
            return Forbid();
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var shift = await db.WorkShifts.FirstOrDefaultAsync(x =>
            x.Id == id && x.VeterinarianId == doctor.Id);

        if (shift is null)
        {
            return NotFound();
        }

        if (shift.StartAt <= DateTime.Now)
        {
            TempData["Error"] = "Không được hủy ca đã bắt đầu.";
            return Back(shift.StartAt);
        }

        var hasAppointment = await db.Appointments.AnyAsync(x =>
            x.VeterinarianId == doctor.Id &&
            x.Status != AppointmentStatus.Canceled &&
            x.StartsAt < shift.EndAt && x.EndsAt > shift.StartAt);

        if (hasAppointment)
        {
            TempData["Error"] =
                "Ca đã có lịch khám. Hãy liên hệ nhân viên để sắp xếp bác sĩ thay thế.";
            return Back(shift.StartAt);
        }

        db.WorkShifts.Remove(shift);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Đã hủy ca làm việc.";
        return Back(shift.StartAt);
    }

    private IActionResult Back(DateTime? date = null)
    {
        var d = date.HasValue && date.Value.Year >= 2020 && date.Value.Year <= 2100
            ? date.Value
            : DateTime.Today;

        return RedirectToAction(nameof(Index), new { year = d.Year, month = d.Month });
    }
}
