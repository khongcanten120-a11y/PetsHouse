
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class ServicesController(AppDbContext db) : Controller
{
    // Danh sách loại khám - sắp xếp theo ID tăng dần
    public async Task<IActionResult> Index()
    {
        var services = await db.ClinicServices
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        return View(services);
    }

    // Hiển thị form thêm loại khám
    [HttpGet]
    public IActionResult Create()
    {
        return View("Form", new ClinicService());
    }

    // Xử lý thêm loại khám
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClinicService vm)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", vm);
        }

        var service = new ClinicService
        {
            Name = vm.Name,
            Description = vm.Description,
            Price = vm.Price,
            DurationMinutes = vm.DurationMinutes,
            IsActive = vm.IsActive
        };

        db.ClinicServices.Add(service);

        await db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm loại khám.";

        return RedirectToAction(nameof(Index));
    }

    // Hiển thị form chỉnh sửa
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await db.ClinicServices.FindAsync(id);

        if (service is null)
        {
            return NotFound();
        }

        return View("Form", service);
    }

    // Xử lý cập nhật loại khám
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClinicService vm)
    {
        if (id != vm.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", vm);
        }

        var service = await db.ClinicServices.FindAsync(id);

        if (service is null)
        {
            return NotFound();
        }

        service.Name = vm.Name;
        service.Description = vm.Description;
        service.Price = vm.Price;
        service.DurationMinutes = vm.DurationMinutes;
        service.IsActive = vm.IsActive;

        await db.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật loại khám.";

        return RedirectToAction(nameof(Index));
    }

    // Ngừng cung cấp loại khám
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id)
    {
        var service = await db.ClinicServices.FindAsync(id);

        if (service is null)
        {
            return NotFound();
        }

        service.IsActive = false;

        await db.SaveChangesAsync();

        TempData["Success"] = "Đã ngừng cung cấp loại khám.";

        return RedirectToAction(nameof(Index));
    }
}
