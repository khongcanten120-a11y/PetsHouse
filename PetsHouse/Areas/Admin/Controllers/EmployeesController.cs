using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class EmployeesController(
    AppDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Doctors = await db.Veterinarians
            .AsNoTracking()
            .Include(x => x.WorkShifts)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var employees = await users.Users
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        return View(employees);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new StaffCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateVm vm)
    {
        if (vm.Role != Roles.Staff &&
            vm.Role != Roles.Doctor &&
            vm.Role != Roles.Warehouse)
        {
            ModelState.AddModelError(nameof(vm.Role), "Chọn vai trò hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            FullName = vm.FullName
        };

        var created = await users.CreateAsync(user, vm.TemporaryPassword);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(vm);
        }

        var role = await users.AddToRoleAsync(user, vm.Role);
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            ModelState.AddModelError("", "Không gán được vai trò.");
            return View(vm);
        }

        if (vm.Role == Roles.Doctor)
        {
            db.Veterinarians.Add(new Veterinarian
            {
                UserId = user.Id,
                FullName = vm.FullName,
                Specialty = vm.Specialty ?? "Thú y tổng quát"
            });
            await db.SaveChangesAsync();
        }

        TempData["Success"] = "Đã tạo tài khoản. Cần đổi mật khẩu sau khi nhận tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    // Giữ endpoint phân ca của Admin, nhưng chỉ chấp nhận hai khung giờ chuẩn.
    // Nên sử dụng giao diện lịch tháng chung tại /Staff/WorkShifts để chọn ca thuận tiện.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Shift(ShiftVm vm)
    {
        var validationError = ShiftCatalog.ValidateFuture(vm.StartAt, vm.EndAt);
        if (validationError is not null)
        {
            ModelState.AddModelError("", validationError);
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join("; ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var doctorExists = await db.Veterinarians.AnyAsync(x =>
            x.Id == vm.DoctorId && x.IsActive);
        if (!doctorExists)
        {
            TempData["Error"] = "Bác sĩ không tồn tại hoặc ngừng hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        var overlap = await db.WorkShifts.AnyAsync(x =>
            x.VeterinarianId == vm.DoctorId &&
            x.StartAt < vm.EndAt && x.EndAt > vm.StartAt);

        if (overlap)
        {
            TempData["Error"] = "Ca làm việc trùng với ca đã có.";
            return RedirectToAction(nameof(Index));
        }

        
        // PETSHOUSE_LIMIT3_ADMIN_BEGIN
        // Admin va Nhan vien CHI xep ca tu dang ky Accepted, toi da 3 bac si/ca.
        if (!ShiftCatalog.IsStandard(vm.StartAt, vm.EndAt))
        {
            TempData["Error"] = "Chi duoc xep Ca 1 (08:00-16:00) va Ca 2 (16:00-20:00).";
            return RedirectToAction(nameof(Index));
        }
        if (!await db.Veterinarians.AnyAsync(x => x.Id == vm.DoctorId && x.IsActive))
        {
            TempData["Error"] = "Bac si khong con hoat dong.";
            return RedirectToAction(nameof(Index));
        }
        var __petsHasRegistration = await db.DoctorShiftRequests.AnyAsync(x =>
            x.VeterinarianId == vm.DoctorId &&
            x.StartAt == vm.StartAt && x.EndAt == vm.EndAt &&
            x.Status == ShiftRequestStatus.Accepted);
        if (!__petsHasRegistration)
        {
            TempData["Error"] = "Bac si phai DANG KY va duoc TIEP NHAN dung ngay/ca truoc khi xep.";
            return RedirectToAction(nameof(Index));
        }
        var __petsAssignedCount = await db.WorkShifts
            .Where(x => x.StartAt < vm.EndAt && x.EndAt > vm.StartAt)
            .Select(x => x.VeterinarianId).Distinct().CountAsync();
        if (__petsAssignedCount >= 3)
        {
            TempData["Error"] = "Ca da DU 3 BAC SI. Khong the xep nguoi thu tu.";
            return RedirectToAction(nameof(Index));
        }
        // PETSHOUSE_LIMIT3_ADMIN_END
db.WorkShifts.Add(new WorkShift
        {
            VeterinarianId = vm.DoctorId,
            StartAt = vm.StartAt,
            EndAt = vm.EndAt
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Đã phân ca làm việc theo lịch hai ca chuẩn.";
        return RedirectToAction(nameof(Index));
    }
}
