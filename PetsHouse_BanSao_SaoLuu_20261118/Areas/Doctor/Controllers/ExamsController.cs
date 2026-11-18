using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Doctor.Controllers;

[Area("Doctor"), Authorize(Roles = Roles.Doctor)]
public class ExamsController(AppDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private Task<int?> DoctorId() => db.Veterinarians
        .Where(x => x.UserId == users.GetUserId(User)).Select(x => (int?)x.Id).SingleOrDefaultAsync();

    [HttpGet]
    public async Task<IActionResult> Create(int id)
    {
        var doctorId = await DoctorId();
        var appt = await db.Appointments.Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == id && x.VeterinarianId == doctorId &&
                x.Status == AppointmentStatus.Waiting);
        if (appt is null) return NotFound();
        ViewBag.Appointment = appt;
        await LoadMedicines();
        return View(new ExamineVm { AppointmentId = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExamineVm vm)
    {
        // Chỉ các dòng kê thuốc có lựa chọn thuốc mới được lưu.
        var lines = (vm.Items ?? new()).Where(x => x.MedicineId.HasValue).ToList();
        if (vm.Items is not null && vm.Items.Any(x => !x.MedicineId.HasValue &&
                (x.Quantity.HasValue || !string.IsNullOrWhiteSpace(x.Directions))))
            ModelState.AddModelError("", "Đã nhập số lượng/hướng dẫn nhưng chưa chọn thuốc.");
        if (lines.Any(x => x.Quantity is null or <= 0 || string.IsNullOrWhiteSpace(x.Directions)))
            ModelState.AddModelError("", "Mỗi dòng thuốc phải có số lượng và hướng dẫn sử dụng.");
        if (lines.Select(x => x.MedicineId).Distinct().Count() != lines.Count)
            ModelState.AddModelError("", "Không được chọn trùng thuốc trong cùng bệnh án.");
        if (!ModelState.IsValid) return await Redisplay(vm);

        var doctorId = await DoctorId();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var appt = await db.Appointments.FirstOrDefaultAsync(x => x.Id == vm.AppointmentId &&
            x.VeterinarianId == doctorId);
        if (appt is null || appt.Status != AppointmentStatus.Waiting ||
            await db.MedicalRecords.AnyAsync(x => x.AppointmentId == vm.AppointmentId))
        {
            TempData["Error"] = "Ca khám không hợp lệ hoặc đã có bệnh án.";
            return RedirectToAction("Index", "Dashboard");
        }
        var ids = lines.Select(x => x.MedicineId!.Value).ToList();
        var medicines = await db.Medicines.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        foreach (var line in lines)
        {
            if (!medicines.TryGetValue(line.MedicineId!.Value, out var med) || !med.IsActive ||
                med.QuantityOnHand < line.Quantity!.Value ||
                (med.ExpiresOn.HasValue && med.ExpiresOn.Value.Date < DateTime.Today))
            {
                ModelState.AddModelError("", "Thuốc không đủ số lượng, bị khóa hoặc đã hết hạn.");
                return await Redisplay(vm);
            }
        }
        var record = new MedicalRecord
        {
            AppointmentId = appt.Id,
            Symptoms = vm.Symptoms.Trim(),
            Diagnosis = vm.Diagnosis.Trim(),
            Treatment = vm.Treatment?.Trim()
        };
        foreach (var line in lines)
        {
            var medicine = medicines[line.MedicineId!.Value];
            record.Prescriptions.Add(new PrescriptionItem
            {
                MedicineId = medicine.Id,
                Quantity = line.Quantity!.Value,
                Directions = line.Directions!.Trim(),
                UnitPrice = medicine.UnitPrice
            });
        }
        db.MedicalRecords.Add(record);
        appt.Status = AppointmentStatus.AwaitingPayment;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Success"] = "Đã lưu bệnh án và chuyển chờ thanh toán. Thuốc sẽ được trừ kho khi cấp phát.";
        return RedirectToAction("Index", "Dashboard");
    }

    private async Task LoadMedicines() => ViewBag.Medicines = await db.Medicines.AsNoTracking()
        .Where(x => x.IsActive && x.QuantityOnHand > 0 &&
            (!x.ExpiresOn.HasValue || x.ExpiresOn.Value >= DateTime.Today))
        .OrderBy(x => x.Name).ToListAsync();

    private async Task<IActionResult> Redisplay(ExamineVm vm)
    {
        var doctorId = await DoctorId();
        ViewBag.Appointment = await db.Appointments.Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == vm.AppointmentId && x.VeterinarianId == doctorId);
        if (ViewBag.Appointment is null) return NotFound();
        await LoadMedicines();
        if (vm.Items is null || vm.Items.Count == 0) vm.Items = new() { new PrescriptionLineVm() };
        return View(vm);
    }
}
