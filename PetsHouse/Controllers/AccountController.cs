using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    AppDbContext db,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return View(vm);

        var user = await users.FindByEmailAsync(vm.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View(vm);
        }

        var result = await signIn.PasswordSignInAsync(user, vm.Password, false, true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.IsLockedOut
                ? "Tài khoản tạm khóa do đăng nhập sai nhiều lần."
                : "Email hoặc mật khẩu không đúng.");
            return View(vm);
        }

        var roles = await users.GetRolesAsync(user);
        var role = roles.Contains(Roles.Admin) ? Roles.Admin
            : roles.Contains(Roles.Staff) ? Roles.Staff
            : roles.Contains(Roles.Doctor) ? Roles.Doctor
            : roles.Contains(Roles.Warehouse) ? Roles.Warehouse
            : roles.Contains(Roles.Customer) ? Roles.Customer
            : null;

        // Không âm thầm chuyển tài khoản chưa được gán vai trò sang khu vực Khách hàng.
        if (role is null)
        {
            logger.LogWarning("PetsHouse: sign-in succeeded but the account has no recognized role.");
            await signIn.SignOutAsync();
            ModelState.AddModelError("", "Tài khoản chưa được phân quyền. Vui lòng liên hệ quản trị viên.");
            return View(vm);
        }

        // Chỉ giữ returnUrl là một trang nội bộ thực sự. Trường hợp '/' hoặc Home
        // phải điều hướng về dashboard tương ứng, tránh đăng nhập xong lại về trang chủ.
        bool hasUsefulReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && returnUrl != "/"
            && !returnUrl.StartsWith("/Home", StringComparison.OrdinalIgnoreCase)
            && !returnUrl.StartsWith("~/Home", StringComparison.OrdinalIgnoreCase)
            && !returnUrl.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase);

        if (hasUsefulReturnUrl)
        {
            logger.LogInformation("PetsHouse: sign-in succeeded for role {Role}; redirecting to requested local URL.", role);
            return LocalRedirect(returnUrl!);
        }

        logger.LogInformation("PetsHouse: sign-in succeeded; redirecting to dashboard for role {Role}.", role);

        return role switch
        {
            Roles.Admin => RedirectToAction("Index", "Dashboard", new { area = "Admin" }),
            Roles.Staff => RedirectToAction("Index", "Dashboard", new { area = "Staff" }),
            Roles.Doctor => RedirectToAction("Index", "Dashboard", new { area = "Doctor" }),
            Roles.Warehouse => RedirectToAction("Index", "Dashboard", new { area = "Warehouse" }),
            Roles.Customer => RedirectToAction("Index", "Customer", new { area = "" }),
            _ => RedirectToAction("Index", "Home", new { area = "" })
        };
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterVm());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FullName = vm.FullName,
            PhoneNumber = vm.Phone
        };

        var result = await users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(vm);
        }

        var addedRole = await users.AddToRoleAsync(user, Roles.Customer);
        if (!addedRole.Succeeded)
        {
            await users.DeleteAsync(user);
            ModelState.AddModelError("", "Không tạo được vai trò khách hàng.");
            return View(vm);
        }

        db.Customers.Add(new Customer
        {
            UserId = user.Id,
            Name = vm.FullName,
            Phone = vm.Phone
        });
        await db.SaveChangesAsync();
        await signIn.SignInAsync(user, false);
        return RedirectToAction("Index", "Customer", new { area = "" });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home", new { area = "" });
    }

    public IActionResult AccessDenied() => View();
}
