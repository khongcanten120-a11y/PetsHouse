using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Controllers;
[Authorize(Roles=Roles.Customer)]
public class PetsController(AppDbContext db,UserManager<ApplicationUser> users):Controller
{
    private string Uid()=>users.GetUserId(User)!;
    private Task<int> CustomerId()=>db.Customers.Where(x=>x.UserId==Uid()).Select(x=>x.Id).SingleAsync();
    public async Task<IActionResult> Index()
    {
        var id=await CustomerId();
        return View(await db.Pets.Where(x=>x.CustomerId==id).OrderBy(x=>x.Name).ToListAsync());
    }
    [HttpGet]public IActionResult Create()=>View("Form",new PetVm());
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PetVm vm)
    {
        if(!ModelState.IsValid)return View("Form",vm);
        db.Pets.Add(new Pet{CustomerId=await CustomerId(),Name=vm.Name,Species=vm.Species,Breed=vm.Breed,Gender=vm.Gender,BirthDate=vm.BirthDate,WeightKg=vm.WeightKg,Notes=vm.Notes});
        await db.SaveChangesAsync();TempData["Success"]="Đã thêm thú cưng!";return RedirectToAction(nameof(Index));
    }
    [HttpGet]public async Task<IActionResult> Edit(int id)
    {
        var customerId=await CustomerId();
        var x=await db.Pets.FirstOrDefaultAsync(x=>x.Id==id&&x.CustomerId==customerId);
        if(x is null)return NotFound();
        return View("Form",new PetVm{Id=x.Id,Name=x.Name,Species=x.Species,Breed=x.Breed,Gender=x.Gender,BirthDate=x.BirthDate,WeightKg=x.WeightKg,Notes=x.Notes});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,PetVm vm)
    {
        if(id!=vm.Id)return BadRequest();
        if(!ModelState.IsValid)return View("Form",vm);
        var customerId=await CustomerId();
        var x=await db.Pets.FirstOrDefaultAsync(x=>x.Id==id&&x.CustomerId==customerId);
        if(x is null)return NotFound();
        x.Name=vm.Name;x.Species=vm.Species;x.Breed=vm.Breed;x.Gender=vm.Gender;x.BirthDate=vm.BirthDate;x.WeightKg=vm.WeightKg;x.Notes=vm.Notes;
        await db.SaveChangesAsync();TempData["Success"]="Đã lưu hồ sơ!";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customerId=await CustomerId();
        var x=await db.Pets.FirstOrDefaultAsync(x=>x.Id==id&&x.CustomerId==customerId);
        if(x is null)return NotFound();
        if(await db.Appointments.AnyAsync(a=>a.PetId==id)) { TempData["Error"]="Thú cưng có lịch sử khám, không thể xóa.";return RedirectToAction(nameof(Index)); }
        db.Pets.Remove(x);await db.SaveChangesAsync();TempData["Success"]="Đã xóa hồ sơ.";return RedirectToAction(nameof(Index));
    }
}
