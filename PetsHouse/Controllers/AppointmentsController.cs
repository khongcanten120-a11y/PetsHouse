using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
using PetsHouse.Services;

namespace PetsHouse.Controllers;

[Authorize(Roles = Roles.Customer)]
public class AppointmentsController(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    BookingService booking) : Controller
{
    private Task<int> CustomerId()
    {
        var userId = users.GetUserId(User);
        return db.Customers
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .SingleAsync();
    }

    public async Task<IActionResult> Index()
    {
        var customerId = await CustomerId();

        var appointments = await db.Appointments
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Include(x => x.Veterinarian)
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.StartsAt)
            .ToListAsync();

        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await Populate();
        return View(new AppointmentVm
        {
            StartsAt = DateTime.Now.AddDays(1).Date.AddHours(9)
        });
    }

    private async Task Populate()
    {
        var customerId = await CustomerId();

        ViewBag.Pets = await db.Pets
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Name)
            .ToListAsync();

        ViewBag.Services = await db.ClinicServices
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        ViewBag.Doctors = await db.Veterinarians
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentVm vm)
    {
        if (ModelState.IsValid)
        {
            var result = await booking.BookAsync(await CustomerId(), vm);

            if (result.Ok)
            {
                TempData["Success"] = "Đã gửi yêu cầu đặt lịch.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", result.Error);
        }

        await Populate();
        return View(vm);
    }

    // Chỉ hủy được các lịch chưa bắt đầu khám, khi còn ít nhất 24 giờ.
    // Kiểm tra trên SQL Server để chặn thao tác sửa request từ trình duyệt.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var customerId = await CustomerId();
        var cutoff = DateTime.Now.AddHours(24);

        // Thao tác UPDATE có điều kiện giúp ngăn hủy lịch đã bắt đầu,
        // đã thanh toán, thuộc khách khác hoặc đã quá hạn hủy.
        var updatedRows = await db.Appointments
            .Where(x => x.Id == id &&
                        x.CustomerId == customerId &&
                        x.StartsAt >= cutoff &&
                        (x.Status == AppointmentStatus.Pending ||
                         x.Status == AppointmentStatus.Confirmed ||
                         x.Status == AppointmentStatus.Waiting))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, AppointmentStatus.Canceled));

        if (updatedRows == 1)
        {
            TempData["Success"] = "Đã hủy lịch khám thành công.";
            return RedirectToAction(nameof(Index));
        }

        var existsForCustomer = await db.Appointments.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.CustomerId == customerId);

        if (!existsForCustomer)
        {
            return NotFound();
        }

        TempData["Error"] =
            "Không thể hủy: lịch phải còn ít nhất 24 giờ và chưa bắt đầu khám, " +
            "chưa hoàn thành hoặc bị hủy trước đó.";

        return RedirectToAction(nameof(Index));
    }
}
