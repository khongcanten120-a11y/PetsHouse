using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Controllers;

public class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db) : Controller
{
    [HttpGet, AllowAnonymous] public IActionResult Login(string? returnUrl=null)
    { ViewBag.ReturnUrl=returnUrl; return View(new LoginVm()); }
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm,string? returnUrl=null)
    {
        if(!ModelState.IsValid) return View(vm);
        var user=await users.FindByEmailAsync(vm.Email);
        if(user is null){ ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");return View(vm); }
        var result=await signIn.PasswordSignInAsync(user,vm.Password,false,true);
        if(!result.Succeeded){ ModelState.AddModelError("",result.IsLockedOut?"Tài khoản tạm khóa do đăng nhập sai nhiều lần.":"Email hoặc mật khẩu không đúng.");return View(vm); }
        if(!string.IsNullOrWhiteSpace(returnUrl)&&Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        var roles=await users.GetRolesAsync(user);
        if(roles.Contains(Roles.Admin))return RedirectToAction("Index","Dashboard",new{area="Admin"});
        if(roles.Contains(Roles.Staff))return RedirectToAction("Index","Dashboard",new{area="Staff"});
        if(roles.Contains(Roles.Doctor))return RedirectToAction("Index","Dashboard",new{area="Doctor"});
        if(roles.Contains(Roles.Warehouse))return RedirectToAction("Index","Dashboard",new{area="Warehouse"});
        return RedirectToAction("Index","Customer");
    }
    [HttpGet,AllowAnonymous] public IActionResult Register()=>View(new RegisterVm());
    [HttpPost,AllowAnonymous,ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if(!ModelState.IsValid)return View(vm);
        var user=new ApplicationUser{UserName=vm.Email,Email=vm.Email,FullName=vm.FullName,PhoneNumber=vm.Phone};
        var r=await users.CreateAsync(user,vm.Password);
        if(!r.Succeeded){foreach(var e in r.Errors)ModelState.AddModelError("",e.Description);return View(vm);}
        var role=await users.AddToRoleAsync(user,Roles.Customer);
        if(!role.Succeeded){await users.DeleteAsync(user);ModelState.AddModelError("","Không tạo được vai trò khách hàng.");return View(vm);}
        db.Customers.Add(new Customer{UserId=user.Id,Name=vm.FullName,Phone=vm.Phone});
        await db.SaveChangesAsync();
        await signIn.SignInAsync(user,false);
        return RedirectToAction("Index","Customer");
    }
    [Authorize,HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(){await signIn.SignOutAsync();return RedirectToAction("Index","Home");}
    public IActionResult AccessDenied()=>View();
}
