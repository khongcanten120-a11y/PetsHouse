using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
namespace PetsHouse.Areas.Staff.Controllers;
[Area("Staff"),Authorize(Roles=Roles.Staff)]
public class PaymentsController(AppDbContext db):Controller
{
    public async Task<IActionResult> Index()=>View(await db.Appointments.Include(x=>x.Pet).Include(x=>x.ClinicService).Include(x=>x.MedicalRecord)!.ThenInclude(x=>x.Prescriptions).Where(x=>x.Status==AppointmentStatus.AwaitingPayment).OrderBy(x=>x.StartsAt).ToListAsync());
    [HttpGet] public async Task<IActionResult> Pay(int id)
    {
        var app=await db.Appointments.Include(x=>x.Pet).Include(x=>x.ClinicService).Include(x=>x.MedicalRecord)!.ThenInclude(x=>x.Prescriptions).FirstOrDefaultAsync(x=>x.Id==id);
        if(app is null||app.Status!=AppointmentStatus.AwaitingPayment)return NotFound();
        ViewBag.Appointment=app;ViewBag.Total=app.ClinicService.Price+(app.MedicalRecord?.Prescriptions.Sum(x=>x.UnitPrice*x.Quantity)??0);
        return View(new PaymentVm{AppointmentId=id,AmountTendered=(decimal)ViewBag.Total});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaymentVm vm)
    {
        if(vm.Method!="Tiền mặt"&&vm.Method!="Chuyển khoản")ModelState.AddModelError(nameof(vm.Method),"Phương thức không hợp lệ.");
        if(!ModelState.IsValid)return await Redisplay(vm);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var appt=await db.Appointments.Include(x=>x.ClinicService).Include(x=>x.MedicalRecord)!.ThenInclude(x=>x.Prescriptions).FirstOrDefaultAsync(x=>x.Id==vm.AppointmentId);
        if(appt is null)return NotFound();
        if(appt.Status!=AppointmentStatus.AwaitingPayment||await db.Invoices.AnyAsync(x=>x.AppointmentId==appt.Id))
        {TempData["Error"]="Ca đã thanh toán hoặc trạng thái không hợp lệ.";return RedirectToAction(nameof(Index));}
        var invoice=new Invoice{AppointmentId=appt.Id,Status=PaymentStatus.Paid,PaymentMethod=vm.Method,AmountTendered=vm.AmountTendered,PaidAtUtc=DateTime.UtcNow};
        invoice.Items.Add(new InvoiceItem{Description=appt.ClinicService.Name,Quantity=1,UnitPrice=appt.ClinicService.Price});
        if(appt.MedicalRecord is not null)
        foreach(var item in appt.MedicalRecord.Prescriptions)
            invoice.Items.Add(new InvoiceItem{Description="Thuốc mã #"+item.MedicineId,Quantity=item.Quantity,UnitPrice=item.UnitPrice});
        invoice.Total=invoice.Items.Sum(x=>x.Quantity*x.UnitPrice);
        if(vm.AmountTendered<invoice.Total){ModelState.AddModelError(nameof(vm.AmountTendered),"Số tiền nhận chưa đủ.");return await Redisplay(vm);}
        db.Invoices.Add(invoice);appt.Status=AppointmentStatus.Completed;
        await db.SaveChangesAsync();await tx.CommitAsync();
        TempData["Success"]="Đã lưu hóa đơn và ghi nhận thanh toán #"+invoice.Id;
        return RedirectToAction(nameof(Receipt), new { id=invoice.Id });
    }
    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(x => x.Appointment).ThenInclude(x => x.Pet)
            .Include(x => x.Appointment).ThenInclude(x => x.Customer)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == PaymentStatus.Paid);
        return invoice is null ? NotFound() : View(invoice);
    }

    private async Task<IActionResult> Redisplay(PaymentVm vm)
    {
        var appt=await db.Appointments.Include(x=>x.Pet).Include(x=>x.ClinicService).Include(x=>x.MedicalRecord)!.ThenInclude(x=>x.Prescriptions).FirstOrDefaultAsync(x=>x.Id==vm.AppointmentId);
        if(appt is null)return NotFound();
        ViewBag.Appointment=appt;ViewBag.Total=appt.ClinicService.Price+(appt.MedicalRecord?.Prescriptions.Sum(x=>x.UnitPrice*x.Quantity)??0);
        return View(vm);
    }
}
