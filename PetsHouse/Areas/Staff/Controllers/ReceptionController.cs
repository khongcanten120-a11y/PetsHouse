using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = Roles.Staff)]
public class ReceptionController(
    AppDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    // Tiếp nhận: ưu tiên tìm hồ sơ cũ trước khi tạo mới.
    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length > 80)
            {
                q = q[..80];
            }

            query = query.Where(customer =>
                customer.Name.Contains(q) ||
                customer.Phone.Contains(q) ||
                (customer.User.Email != null && customer.User.Email.Contains(q)));
        }

        ViewBag.Query = q;
        return View(await query
            .Include(customer => customer.User)
            .Include(customer => customer.Pets)
            .OrderBy(customer => customer.Id)
            .Take(100)
            .ToListAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new StaffCustomerCreateVm());
    }

    // Tạo tài khoản chỉ với quyền Customer, đồng thời tạo chủ nuôi + thú cưng.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCustomerCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        vm.Email = vm.Email.Trim();
        vm.Phone = vm.Phone.Trim();
        vm.FullName = vm.FullName.Trim();
        vm.PetName = vm.PetName.Trim();
        vm.Species = vm.Species.Trim();

        if (await db.Customers.AnyAsync(customer => customer.Phone == vm.Phone))
        {
            ModelState.AddModelError(nameof(vm.Phone),
                "Số điện thoại đã có hồ sơ. Hãy tra cứu và thêm thú cưng vào hồ sơ cũ.");
            return View(vm);
        }

        if (await users.FindByEmailAsync(vm.Email) is not null)
        {
            ModelState.AddModelError(nameof(vm.Email), "Email đã có tài khoản.");
            return View(vm);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FullName = vm.FullName,
            PhoneNumber = vm.Phone,
            EmailConfirmed = false
        };

        var created = await users.CreateAsync(user, vm.TemporaryPassword);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(vm);
        }

        var role = await users.AddToRoleAsync(user, Roles.Customer);
        if (!role.Succeeded)
        {
            foreach (var error in role.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(vm);
        }

        var customer = new Customer
        {
            UserId = user.Id,
            Name = vm.FullName,
            Phone = vm.Phone,
            Address = vm.Address?.Trim()
        };

        customer.Pets.Add(new Pet
        {
            Name = vm.PetName,
            Species = vm.Species,
            Breed = vm.Breed?.Trim(),
            Gender = vm.Gender
        });

        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = "Đã tạo tài khoản khách hàng và hồ sơ thú cưng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AddPet(int customerId)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId);
        if (customer is null)
        {
            return NotFound();
        }

        ViewBag.CustomerName = customer.Name;
        return View(new StaffPetCreateVm { CustomerId = customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPet(StaffPetCreateVm vm)
    {
        if (!await db.Customers.AnyAsync(x => x.Id == vm.CustomerId))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        db.Pets.Add(new Pet
        {
            CustomerId = vm.CustomerId,
            Name = vm.Name.Trim(),
            Species = vm.Species.Trim(),
            Breed = vm.Breed?.Trim(),
            Gender = vm.Gender,
            BirthDate = vm.BirthDate,
            WeightKg = vm.WeightKg,
            Notes = vm.Notes?.Trim()
        });

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm hồ sơ thú cưng.";
        return RedirectToAction(nameof(Index));
    }
}
