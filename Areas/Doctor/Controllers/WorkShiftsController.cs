using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Doctor.Controllers;

public class DoctorShiftRegistrationVm
{
    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    public DateTime? EndAt { get; set; }
}

[Area("Doctor")]
[Authorize(Roles = Roles.Doctor)]
public class WorkShiftsController(
    AppDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    private Task<Veterinarian?> CurrentDoctorAsync()
    {
        var userId = users.GetUserId(User);

        return db.Veterinarians
            .FirstOrDefaultAsync(doctor => doctor.UserId == userId && doctor.IsActive);
    }

    // Bác sĩ xem các ca làm việc của chính mình.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var doctor = await CurrentDoctorAsync();

        if (doctor is null)
        {
            ViewBag.HasDoctorProfile = false;
            return View(new List<WorkShift>());
        }

        ViewBag.HasDoctorProfile = true;
        ViewBag.DoctorName = doctor.FullName;

        var earliest = DateTime.Today.AddDays(-30);
        var shifts = await db.WorkShifts
            .AsNoTracking()
            .Where(shift =>
                shift.VeterinarianId == doctor.Id &&
                shift.EndAt >= earliest)
            .OrderBy(shift => shift.StartAt)
            .ThenBy(shift => shift.Id)
            .Take(200)
            .ToListAsync();

        return View(shifts);
    }

    // Đăng ký ca mới, bác sĩ không thể tự chọn DoctorId của người khác.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DoctorShiftRegistrationVm vm)
    {
        var doctor = await CurrentDoctorAsync();

        if (doctor is null)
        {
            TempData["Error"] = "Tài khoản chưa có hồ sơ bác sĩ đang hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid || !vm.StartAt.HasValue || !vm.EndAt.HasValue)
        {
            TempData["Error"] = "Vui lòng nhập đủ ngày và giờ bắt đầu, kết thúc.";
            return RedirectToAction(nameof(Index));
        }

        var start = vm.StartAt.Value;
        var end = vm.EndAt.Value;
        var now = DateTime.Now;

        if (start <= now)
        {
            TempData["Error"] = "Giờ bắt đầu phải ở tương lai.";
            return RedirectToAction(nameof(Index));
        }

        if (end <= start)
        {
            TempData["Error"] = "Giờ kết thúc phải sau giờ bắt đầu.";
            return RedirectToAction(nameof(Index));
        }

        var duration = end - start;
        if (duration < TimeSpan.FromMinutes(30) || duration > TimeSpan.FromHours(12))
        {
            TempData["Error"] = "Mỗi ca phải dài từ 30 phút đến 12 giờ.";
            return RedirectToAction(nameof(Index));
        }

        if (start > now.AddDays(180))
        {
            TempData["Error"] = "Chỉ đăng ký lịch trong 180 ngày tới.";
            return RedirectToAction(nameof(Index));
        }

        // Kiểm tra và ghi dữ liệu cùng transaction để giảm nguy cơ đăng ký trùng.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var overlaps = await db.WorkShifts.AnyAsync(shift =>
            shift.VeterinarianId == doctor.Id &&
            shift.StartAt < end &&
            shift.EndAt > start);

        if (overlaps)
        {
            TempData["Error"] = "Khung giờ này trùng với ca làm việc đã có.";
            return RedirectToAction(nameof(Index));
        }

        db.WorkShifts.Add(new WorkShift
        {
            VeterinarianId = doctor.Id,
            StartAt = start,
            EndAt = end
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = "Đăng ký lịch làm việc thành công.";
        return RedirectToAction(nameof(Index));
    }

    // Chỉ hủy ca của bản thân; không hủy ca đã được dùng để phân công khám.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var doctor = await CurrentDoctorAsync();

        if (doctor is null)
        {
            return Forbid();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var shift = await db.WorkShifts.FirstOrDefaultAsync(x =>
            x.Id == id && x.VeterinarianId == doctor.Id);

        if (shift is null)
        {
            return NotFound();
        }

        if (shift.StartAt <= DateTime.Now)
        {
            TempData["Error"] = "Chỉ được hủy ca chưa bắt đầu.";
            return RedirectToAction(nameof(Index));
        }

        var hasAppointment = await db.Appointments.AnyAsync(appt =>
            appt.VeterinarianId == doctor.Id &&
            appt.Status != AppointmentStatus.Canceled &&
            appt.StartsAt < shift.EndAt &&
            appt.EndsAt > shift.StartAt);

        if (hasAppointment)
        {
            TempData["Error"] =
                "Ca này đã có lịch khám được phân công. Hãy liên hệ nhân viên để sắp xếp lại.";
            return RedirectToAction(nameof(Index));
        }

        db.WorkShifts.Remove(shift);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = "Đã hủy ca làm việc.";
        return RedirectToAction(nameof(Index));
    }
}
