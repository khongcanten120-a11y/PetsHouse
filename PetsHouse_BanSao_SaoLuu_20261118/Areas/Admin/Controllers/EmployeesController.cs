using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Areas.Admin.Controllers;
[Area("Admin"),Authorize(Roles=Roles.Admin)]
public class EmployeesController(AppDbContext db,UserManager<ApplicationUser> users):Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Doctors=await db.Veterinarians.Include(x=>x.WorkShifts).OrderBy(x=>x.FullName).ToListAsync();
        return View(await users.Users.OrderBy(x=>x.FullName).ToListAsync());
    }
    [HttpGet]public IActionResult Create()=>View(new StaffCreateVm());
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateVm vm)
    {
        if(vm.Role!=Roles.Staff&&vm.Role!=Roles.Doctor&&vm.Role!=Roles.Warehouse)
            ModelState.AddModelError(nameof(vm.Role),"Chọn vai trò hợp lệ.");
        if(!ModelState.IsValid)return View(vm);
        var u=new ApplicationUser{UserName=vm.Email,Email=vm.Email,EmailConfirmed=true,FullName=vm.FullName};
        var created=await users.CreateAsync(u,vm.TemporaryPassword);
        if(!created.Succeeded){foreach(var e in created.Errors)ModelState.AddModelError("",e.Description);return View(vm);}
        var role=await users.AddToRoleAsync(u,vm.Role);
        if(!role.Succeeded){await users.DeleteAsync(u);ModelState.AddModelError("","Không gán được vai trò.");return View(vm);}
        if(vm.Role==Roles.Doctor)
        {
            db.Veterinarians.Add(new Veterinarian{UserId=u.Id,FullName=vm.FullName,Specialty=vm.Specialty??"Thú y tổng quát"});
            await db.SaveChangesAsync();
        }
        TempData["Success"]="Đã tạo tài khoản. Cần đổi mật khẩu sau khi nhận tài khoản.";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Shift(ShiftVm vm)
    {
        if(vm.StartAt>=vm.EndAt)ModelState.AddModelError("","Giờ kết thúc phải sau giờ bắt đầu.");
        if(!await db.Veterinarians.AnyAsync(x=>x.Id==vm.DoctorId))ModelState.AddModelError("","Bác sĩ không tồn tại.");
        if(!ModelState.IsValid){TempData["Error"]=string.Join("; ",ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage));return RedirectToAction(nameof(Index));}
        var overlap=await db.WorkShifts.AnyAsync(x=>x.VeterinarianId==vm.DoctorId&&x.StartAt<vm.EndAt&&x.EndAt>vm.StartAt);
        if(overlap){TempData["Error"]="Ca làm việc trùng với ca có sẵn.";return RedirectToAction(nameof(Index));}
        db.WorkShifts.Add(new WorkShift{VeterinarianId=vm.DoctorId,StartAt=vm.StartAt,EndAt=vm.EndAt});await db.SaveChangesAsync();
        TempData["Success"]="Đã phân ca làm việc.";return RedirectToAction(nameof(Index));
    }
}
