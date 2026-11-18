using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Warehouse.Controllers;

[Area("Warehouse")]
[Authorize(Roles = Roles.Warehouse)]
public class DashboardController(AppDbContext db) : Controller
{
    // Danh sách kho: thống nhất sắp xếp theo mã ID tăng dần.
    public async Task<IActionResult> Index()
    {
        ViewBag.Prescriptions = await db.PrescriptionItems
            .AsNoTracking()
            .Include(item => item.Medicine)
            .Include(item => item.MedicalRecord)
            .Where(item => item.DispensedAtUtc == null)
            .OrderBy(item => item.Id)
            .Take(100)
            .ToListAsync();

        var medicines = await db.Medicines
            .AsNoTracking()
            .OrderBy(medicine => medicine.Id)
            .ToListAsync();

        return View(medicines);
    }

    // Giữ nguyên chức năng tạo danh mục thuốc.
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Medicine());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Medicine vm)
    {
        if (vm.QuantityOnHand != 0)
        {
            ModelState.AddModelError(
                nameof(vm.QuantityOnHand),
                "Khi thêm thuốc, tồn kho phải bằng 0; hãy dùng chức năng nhập kho.");
        }

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        db.Medicines.Add(new Medicine
        {
            Name = vm.Name,
            Unit = vm.Unit,
            UnitPrice = vm.UnitPrice,
            QuantityOnHand = 0,
            ReorderLevel = vm.ReorderLevel,
            ExpiresOn = vm.ExpiresOn,
            IsActive = true
        });

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm danh mục thuốc.";
        return RedirectToAction(nameof(Index));
    }

    // Nhập kho: giữ hành vi nghiệp vụ hiện tại.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(ReceiveVm vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nhập số lượng dương và thuốc hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var medicine = await db.Medicines.FindAsync(vm.MedicineId);
        if (medicine is null)
        {
            return NotFound();
        }

        if (!medicine.IsActive ||
            (medicine.ExpiresOn.HasValue && medicine.ExpiresOn.Value.Date < DateTime.Today))
        {
            TempData["Error"] = "Không nhập thuốc hết hạn hoặc ngừng hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        if (medicine.QuantityOnHand > int.MaxValue - vm.Quantity)
        {
            TempData["Error"] = "Số lượng nhập làm vượt giới hạn tồn kho.";
            return RedirectToAction(nameof(Index));
        }

        medicine.QuantityOnHand += vm.Quantity;

        db.StockMovements.Add(new StockMovement
        {
            MedicineId = medicine.Id,
            QuantityChange = vm.Quantity,
            Reason = "Nhập kho",
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Nhập kho thành công; đã cập nhật tồn kho và nhật ký.";
        return RedirectToAction(nameof(Index));
    }

    // MỚI: Xuất kho thủ công để điều chuyển, kiểm kê, tiêu hủy.
    // Không sử dụng action này để cấp phát thuốc theo đơn bác sĩ.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportStock(
        int medicineId,
        int quantity,
        string? reason)
    {
        reason = reason?.Trim();

        if (medicineId <= 0 || quantity <= 0 || quantity > 999999)
        {
            TempData["Error"] = "Chọn thuốc và số lượng xuất hợp lệ (lớn hơn 0).";
            return RedirectToAction(nameof(Index));
        }

        // Chỉ cho phép các lý do xuất kho thủ công đã định nghĩa.
        var validReason = reason is
            "Điều chuyển kho" or
            "Điều chỉnh giảm sau kiểm kê" or
            "Tiêu hủy thuốc hết hạn" or
            "Tiêu hủy thuốc hỏng";

        if (!validReason)
        {
            TempData["Error"] = "Bạn phải chọn lý do xuất kho hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        // Cập nhật trực tiếp với điều kiện tồn kho >= số lượng xuất.
        // SQL Server xử lý điều kiện và phép trừ trong cùng một câu lệnh,
        // tránh việc hai người cùng xuất khiến tồn kho âm.
        var updated = await db.Medicines
            .Where(medicine =>
                medicine.Id == medicineId &&
                medicine.QuantityOnHand >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    medicine => medicine.QuantityOnHand,
                    medicine => medicine.QuantityOnHand - quantity));

        if (updated == 0)
        {
            var exists = await db.Medicines
                .AsNoTracking()
                .AnyAsync(medicine => medicine.Id == medicineId);

            TempData["Error"] = exists
                ? "Không đủ số lượng tồn kho để xuất. Vui lòng tải lại danh sách."
                : "Không tìm thấy thuốc cần xuất.";

            // Chưa Commit: mọi thay đổi không được ghi nhận.
            return RedirectToAction(nameof(Index));
        }

        db.StockMovements.Add(new StockMovement
        {
            MedicineId = medicineId,
            QuantityChange = -quantity,
            Reason = "Xuất kho - " + reason,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] =
            $"Đã xuất {quantity} đơn vị và ghi nhật ký xuất kho.";

        return RedirectToAction(nameof(Index));
    }

    // Cấp phát theo đơn thuốc: giữ nguyên chức năng hiện tại.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispense(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var item = await db.PrescriptionItems
            .Include(prescription => prescription.Medicine)
            .FirstOrDefaultAsync(prescription => prescription.Id == id);

        if (item is null)
        {
            return NotFound();
        }

        if (item.DispensedAtUtc.HasValue)
        {
            TempData["Error"] = "Đơn này đã cấp phát.";
            return RedirectToAction(nameof(Index));
        }

        if (item.Medicine.QuantityOnHand < item.Quantity ||
            (item.Medicine.ExpiresOn.HasValue &&
             item.Medicine.ExpiresOn.Value.Date < DateTime.Today))
        {
            TempData["Error"] = "Không đủ thuốc hoặc thuốc đã hết hạn.";
            return RedirectToAction(nameof(Index));
        }

        item.Medicine.QuantityOnHand -= item.Quantity;
        item.DispensedAtUtc = DateTime.UtcNow;

        db.StockMovements.Add(new StockMovement
        {
            MedicineId = item.MedicineId,
            QuantityChange = -item.Quantity,
            Reason = "Cấp phát đơn #" + item.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = "Đã cấp phát đơn thuốc và cập nhật tồn kho.";
        return RedirectToAction(nameof(Index));
    }
}
