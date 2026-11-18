using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Areas.Warehouse.Controllers;
[Area("Warehouse"),Authorize(Roles=Roles.Warehouse)]
public class DashboardController(AppDbContext db):Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Prescriptions=await db.PrescriptionItems.Include(x=>x.Medicine).Include(x=>x.MedicalRecord).Where(x=>x.DispensedAtUtc==null).OrderBy(x=>x.Id).Take(100).ToListAsync();
        return View(await db.Medicines.OrderBy(x=>x.Name).ToListAsync());
    }
    [HttpGet]public IActionResult Create()=>View(new Medicine());
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Medicine vm)
    {
        if(vm.QuantityOnHand!=0)ModelState.AddModelError(nameof(vm.QuantityOnHand),"Khi thêm thuốc, tồn kho phải bằng 0; nhập kho qua phiếu nhận.");
        if(!ModelState.IsValid)return View(vm);
        db.Medicines.Add(new Medicine{Name=vm.Name,Unit=vm.Unit,UnitPrice=vm.UnitPrice,QuantityOnHand=0,ReorderLevel=vm.ReorderLevel,ExpiresOn=vm.ExpiresOn,IsActive=true});
        await db.SaveChangesAsync();TempData["Success"]="Đã thêm danh mục thuốc.";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(ReceiveVm vm)
    {
        if(!ModelState.IsValid){TempData["Error"]="Nhập số lượng dương và thuốc hợp lệ.";return RedirectToAction(nameof(Index));}
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var med=await db.Medicines.FindAsync(vm.MedicineId);if(med is null)return NotFound();
        if(!med.IsActive||med.ExpiresOn.HasValue&&med.ExpiresOn.Value.Date<DateTime.Today){TempData["Error"]="Không nhập thuốc đã hết hạn hoặc bị khóa.";return RedirectToAction(nameof(Index));}
        med.QuantityOnHand+=vm.Quantity;
        db.StockMovements.Add(new StockMovement{MedicineId=med.Id,QuantityChange=vm.Quantity,Reason="Nhập kho"});
        await db.SaveChangesAsync();await tx.CommitAsync();TempData["Success"]="Nhập kho thành công.";return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispense(int id)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var item=await db.PrescriptionItems.Include(x=>x.Medicine).FirstOrDefaultAsync(x=>x.Id==id);
        if(item is null)return NotFound();
        if(item.DispensedAtUtc.HasValue){TempData["Error"]="Đơn này đã cấp phát.";return RedirectToAction(nameof(Index));}
        if(item.Medicine.QuantityOnHand<item.Quantity||item.Medicine.ExpiresOn.HasValue&&item.Medicine.ExpiresOn.Value.Date<DateTime.Today)
        {TempData["Error"]="Không đủ thuốc hoặc thuốc đã hết hạn.";return RedirectToAction(nameof(Index));}
        item.Medicine.QuantityOnHand-=item.Quantity;item.DispensedAtUtc=DateTime.UtcNow;
        db.StockMovements.Add(new StockMovement{MedicineId=item.MedicineId,QuantityChange=-item.Quantity,Reason="Cấp phát đơn #"+item.Id});
        await db.SaveChangesAsync();await tx.CommitAsync();TempData["Success"]="Đã cấp phát và cập nhật tồn kho.";return RedirectToAction(nameof(Index));
    }
}
