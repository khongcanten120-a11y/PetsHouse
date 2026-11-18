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
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        if (y < 2020 || y > 2100 || m < 1 || m > 12)
        {
            return RedirectToAction(nameof(Index));
        }

        var doctor = await CurrentDoctorAsync();
        var gridStart = ShiftCalendarFactory.GetGridStart(y, m);
        var gridEnd = gridStart.AddDays(42);

        var shifts = doctor is null
            ? new List<WorkShift>()
            : await db.WorkShifts.AsNoTracking()
                .Include(x => x.Veterinarian)
                .Where(x => x.VeterinarianId == doctor.Id &&
                            x.StartAt < gridEnd && x.EndAt > gridStart)
                .ToListAsync();

        var requests = doctor is null
            ? new List<DoctorShiftRequest>()
            : await db.DoctorShiftRequests.AsNoTracking()
                .Include(x => x.Veterinarian)
                .Where(x => x.VeterinarianId == doctor.Id &&
                            x.StartAt >= gridStart && x.StartAt < gridEnd)
                .ToListAsync();

        var vm = ShiftCalendarFactory.Build(
            y, m, shifts,
            doctor is null ? new List<Veterinarian>() : new List<Veterinarian> { doctor },
            requests);

        vm.HasDoctorProfile = doctor is not null;
        vm.CurrentDoctorName = doctor?.FullName;
        return View(vm);
    }

    // Đăng ký mới chỉ tạo yêu cầu Pending, CHƯA sinh WorkShift.
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
            TempData["Error"] = "Ngày hoặc ca đăng ký không hợp lệ.";
            return Back(vm.WorkDate);
        }

        var error = ShiftCatalog.ValidateFuture(start, end);
        if (error is not null)
        {
            TempData["Error"] = error;
            return Back(start);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var hasWorkShift = await db.WorkShifts.AnyAsync(x =>
            x.VeterinarianId == doctor.Id && x.StartAt < end && x.EndAt > start);

        if (hasWorkShift)
        {
            TempData["Error"] = "Bạn đã được phân công ca này hoặc có ca trùng giờ.";
            return Back(start);
        }

        var overlappingRequest = await db.DoctorShiftRequests.AnyAsync(x =>
            x.VeterinarianId == doctor.Id &&
            (x.Status == ShiftRequestStatus.Pending ||
             x.Status == ShiftRequestStatus.Accepted ||
             x.Status == ShiftRequestStatus.Assigned) &&
            x.StartAt < end && x.EndAt > start);

        if (overlappingRequest)
        {
            TempData["Error"] = "Bạn đã có yêu cầu đăng ký cho khung giờ này.";
            return Back(start);
        }

        var old = await db.DoctorShiftRequests.FirstOrDefaultAsync(x =>
            x.VeterinarianId == doctor.Id &&
            x.StartAt == start && x.EndAt == end);

        if (old is null)
        {
            db.DoctorShiftRequests.Add(new DoctorShiftRequest
            {
                VeterinarianId = doctor.Id,
                StartAt = start,
                EndAt = end,
                Status = ShiftRequestStatus.Pending
            });
        }
        else
        {
            // Chỉ được đăng ký lại yêu cầu đã bị từ chối / hủy / thay thế.
            old.Status = ShiftRequestStatus.Pending;
            old.CreatedAtUtc = DateTime.UtcNow;
            old.ReviewedAtUtc = null;
            old.ReviewedByUserId = null;
            old.ReviewNote = null;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Đã gửi đăng ký ca. Nhân viên cần tiếp nhận và phân công.";
        return Back(start);
    }

    // Bác sĩ chỉ tự rút yêu cầu chưa phân công; ca đã phân công phải nhờ nhân viên xử lý.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var doctor = await CurrentDoctorAsync();
        if (doctor is null)
        {
            return Forbid();
        }

        var request = await db.DoctorShiftRequests.FirstOrDefaultAsync(x =>
            x.Id == id && x.VeterinarianId == doctor.Id);

        if (request is null)
        {
            return NotFound();
        }

        if (request.StartAt <= DateTime.Now ||
            (request.Status != ShiftRequestStatus.Pending &&
             request.Status != ShiftRequestStatus.Accepted))
        {
            TempData["Error"] = "Chỉ có thể rút yêu cầu chưa phân công và chưa bắt đầu.";
            return Back(request.StartAt);
        }

        var withdrawnAt = DateTime.UtcNow;
        var count = await db.DoctorShiftRequests
            .Where(x => x.Id == request.Id &&
                        x.VeterinarianId == doctor.Id &&
                        (x.Status == ShiftRequestStatus.Pending ||
                         x.Status == ShiftRequestStatus.Accepted))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ShiftRequestStatus.Withdrawn)
                .SetProperty(x => x.ReviewNote, "Bác sĩ tự rút yêu cầu.")
                .SetProperty(x => x.ReviewedAtUtc, withdrawnAt));

        if (count != 1)
        {
            TempData["Error"] = "Yêu cầu vừa được phân công hoặc xử lý bởi nhân viên.";
            return Back(request.StartAt);
        }

        TempData["Success"] = "Đã rút đăng ký ca.";
        return Back(request.StartAt);
    }

    private IActionResult Back(DateTime? date)
    {
        var d = date.HasValue && date.Value.Year >= 2020 && date.Value.Year <= 2100
            ? date.Value : DateTime.Today;
        return RedirectToAction(nameof(Index), new { year = d.Year, month = d.Month });
    }
}
