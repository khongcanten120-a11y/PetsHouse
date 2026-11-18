using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Controllers;

[Authorize(Roles = Roles.Customer)]
public class CustomerController(AppDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var uid = users.GetUserId(User);
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == uid);
        if (customer is null) return NotFound("Không tìm thấy hồ sơ khách hàng.");
        var appointments = db.Appointments.AsNoTracking().Where(x => x.CustomerId == customer.Id);
        var vm = new CustomerDashboardVm
        {
            CustomerName = customer.Name,
            PetCount = await db.Pets.CountAsync(x => x.CustomerId == customer.Id),
            AppointmentCount = await appointments.CountAsync(),
            UpcomingCount = await appointments.CountAsync(x => x.StartsAt >= DateTime.Now &&
                    x.Status != AppointmentStatus.Canceled && x.Status != AppointmentStatus.Completed),
            RecentAppointments = await appointments.Include(x => x.Pet).Include(x => x.ClinicService)
                .OrderByDescending(x => x.StartsAt).Take(5).ToListAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var uid = users.GetUserId(User);
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == uid);
        if (customer is null) return NotFound();
        return View(new CustomerProfileVm { Name = customer.Name, Phone = customer.Phone, Address = customer.Address });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(CustomerProfileVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var uid = users.GetUserId(User);
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.UserId == uid);
        if (customer is null) return NotFound();
        customer.Name = vm.Name.Trim();
        customer.Phone = vm.Phone.Trim();
        customer.Address = vm.Address?.Trim();
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật thông tin khách hàng.";
        return RedirectToAction(nameof(Profile));
    }
}
