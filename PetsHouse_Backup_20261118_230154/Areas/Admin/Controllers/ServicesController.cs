using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Areas.Admin.Controllers;
[Area("Admin"),Authorize(Roles=Roles.Admin)]
public class ServicesController(AppDbContext db):Controller
{
    public async Task<IActionResult> Index()=>View(await db.ClinicServices.OrderBy(x=>x.Name).ToListAsync());
    [HttpGet]public IActionResult Create()=>View("Form",new ClinicService());
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClinicService vm)
    {
        if(!ModelState.IsValid)return View("Form",vm);
        db.ClinicServices.Add(new ClinicService{Name=vm.Name,Description=vm.Description,Price=vm.Price,DurationMinutes=vm.DurationMinutes,IsActive=vm.IsActive});
        await db.SaveChangesAsync();TempData["Success"]="Đã thêm dịch vụ.";return RedirectToAction(nameof(Index));
    }
    [HttpGet]public async Task<IActionResult> Edit(int id)
    {var x=await db.ClinicServices.FindAsync(id);return x is null?NotFound():View("Form",x);}
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,ClinicService vm)
    {
        if(id!=vm.Id)return BadRequest();
        if(!ModelState.IsValid)return View("Form",vm);
        var x=await db.ClinicServices.FindAsync(id);if(x is null)return NotFound();
        x.Name=vm.Name;x.Description=vm.Description;x.Price=vm.Price;x.DurationMinutes=vm.DurationMinutes;x.IsActive=vm.IsActive;
        await db.SaveChangesAsync();TempData["Success"]="Đã cập nhật dịch vụ.";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id)
    {var x=await db.ClinicServices.FindAsync(id);if(x is null)return NotFound();x.IsActive=false;await db.SaveChangesAsync();return RedirectToAction(nameof(Index));}
}
