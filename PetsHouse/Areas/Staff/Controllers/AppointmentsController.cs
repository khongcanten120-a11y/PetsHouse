using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
using PetsHouse.Services;

namespace PetsHouse.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = Roles.Staff)]
public class AppointmentsController(
    AppDbContext db,
    BookingService booking) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        AppointmentStatus? status,
        string? q,
        int page = 1)
    {
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 20;

        var query = db.Appointments.AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length > 80)
            {
                q = q[..80];
            }
            query = query.Where(x =>
                x.Customer.Name.Contains(q) ||
                x.Customer.Phone.Contains(q) ||
                x.Pet.Name.Contains(q));
        }

        ViewBag.Status = status;
        ViewBag.Query = q;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = await query.CountAsync();

        return View(await query
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Include(x => x.Veterinarian)
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync());
    }

    private async Task LoadCreateChoicesAsync()
    {
        ViewBag.Customers = await db.Customers.AsNoTracking()
            .OrderBy(x => x.Id).ToListAsync();
        ViewBag.Pets = await db.Pets.AsNoTracking()
            .OrderBy(x => x.Id).ToListAsync();
        ViewBag.Services = await db.ClinicServices.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        ViewBag.Doctors = await db.Veterinarians.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId)
    {
        await LoadCreateChoicesAsync();
        return View(new StaffAppointmentCreateVm
        {
            CustomerId = customerId ?? 0
        });
    }

    // Đặt lịch tại quầy: tái sử dụng BookingService để kiểm tra trùng bác sĩ.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffAppointmentCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadCreateChoicesAsync();
            return View(vm);
        }

        if (!await db.Customers.AnyAsync(x => x.Id == vm.CustomerId))
        {
            ModelState.AddModelError(nameof(vm.CustomerId), "Khách hàng không tồn tại.");
            await LoadCreateChoicesAsync();
            return View(vm);
        }

        var result = await booking.BookAsync(vm.CustomerId, new AppointmentVm
        {
            PetId = vm.PetId,
            ServiceId = vm.ServiceId,
            VeterinarianId = vm.VeterinarianId,
            StartsAt = vm.StartsAt,
            Notes = vm.Notes
        });

        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            await LoadCreateChoicesAsync();
            return View(vm);
        }

        TempData["Success"] =
            "Đã ghi nhận lịch hẹn; chưa thu cọc. Hãy xác nhận và phân công tại Tiếp nhận lịch khám.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadDoctorsAsync()
    {
        ViewBag.Doctors = await db.Veterinarians.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment is null)
        {
            return NotFound();
        }

        if (!CanChange(appointment.Status) || appointment.StartsAt <= DateTime.Now)
        {
            TempData["Error"] = "Không thể thay đổi ca đã bắt đầu hoặc đã khám.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Appointment = appointment;
        await LoadDoctorsAsync();
        return View(new StaffAppointmentEditVm
        {
            Id = appointment.Id,
            StartsAt = appointment.StartsAt,
            VeterinarianId = appointment.VeterinarianId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StaffAppointmentEditVm vm)
    {
        if (!ModelState.IsValid || vm.StartsAt <= DateTime.Now.AddMinutes(5))
        {
            TempData["Error"] = "Chọn giờ khám hợp lệ, lớn hơn hiện tại ít nhất 5 phút.";
            return RedirectToAction(nameof(Edit), new { id = vm.Id });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var appointment = await db.Appointments
            .Include(x => x.ClinicService)
            .FirstOrDefaultAsync(x => x.Id == vm.Id);

        if (appointment is null)
        {
            return NotFound();
        }

        if (!CanChange(appointment.Status) || appointment.StartsAt <= DateTime.Now)
        {
            TempData["Error"] = "Ca này đã bắt đầu hoặc không được phép thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        var finish = vm.StartsAt.AddMinutes(appointment.ClinicService.DurationMinutes);
        if (vm.VeterinarianId.HasValue)
        {
            var doctorId = vm.VeterinarianId.Value;
            var doctorActive = await db.Veterinarians.AnyAsync(x =>
                x.Id == doctorId && x.IsActive);
            var inShift = await db.WorkShifts.AnyAsync(x =>
                x.VeterinarianId == doctorId &&
                x.StartAt <= vm.StartsAt && x.EndAt >= finish);
            var busy = await db.Appointments.AnyAsync(x =>
                x.Id != appointment.Id &&
                x.VeterinarianId == doctorId &&
                x.Status != AppointmentStatus.Canceled &&
                x.Status != AppointmentStatus.Completed &&
                x.StartsAt < finish && x.EndsAt > vm.StartsAt);

            if (!doctorActive || !inShift || busy)
            {
                TempData["Error"] = "Bác sĩ không hoạt động, không có ca hoặc đã bận giờ này.";
                return RedirectToAction(nameof(Edit), new { id = vm.Id });
            }
        }
        else if (appointment.Status == AppointmentStatus.Waiting)
        {
            TempData["Error"] =
                "Ca đang Chờ khám cần có bác sĩ. Hãy chọn bác sĩ hoặc chuyển lịch với nhân viên.";
            return RedirectToAction(nameof(Edit), new { id = vm.Id });
        }

        appointment.StartsAt = vm.StartsAt;
        appointment.EndsAt = finish;
        appointment.VeterinarianId = vm.VeterinarianId;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = "Đã cập nhật ngày giờ và bác sĩ của lịch hẹn.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is null)
        {
            return NotFound();
        }

        if (!CanChange(appointment.Status) || appointment.StartsAt <= DateTime.Now)
        {
            TempData["Error"] = "Không thể hủy lịch đang khám hoặc đã hoàn thành.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = AppointmentStatus.Canceled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            var note = "[Nhân viên hủy lịch: " + reason.Trim()[..Math.Min(reason.Trim().Length, 160)] + "]";
            var combined = string.IsNullOrWhiteSpace(appointment.Notes)
                ? note
                : appointment.Notes + "\n" + note;
            appointment.Notes = combined[..Math.Min(combined.Length, 600)];
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Đã hủy lịch hẹn. Hồ sơ vẫn được lưu để tra cứu.";
        return RedirectToAction(nameof(Index));
    }

    private static bool CanChange(AppointmentStatus status)
    {
        return status is AppointmentStatus.Pending or
            AppointmentStatus.Confirmed or AppointmentStatus.Waiting;
    }
}
