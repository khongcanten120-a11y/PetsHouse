using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ReportsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? year)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2020, 2100);
        return View(await ReadAsync(y));
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? year)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2020, 2100);
        var report = await ReadAsync(y);
        var sb = new StringBuilder("\uFEFFThang,So_hoa_don,Doanh_thu_VND\r\n");
        foreach (var item in report.Months)
            sb.Append(item.Month).Append(',').Append(item.InvoiceCount).Append(',')
                .Append(item.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
        // Báo cáo chỉ gồm số. Không đưa chuỗi do người dùng nhập vào công thức CSV.
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"PetsHouse_DoanhThu_{y}.csv");
    }

    private async Task<ReportsVm> ReadAsync(int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        var appts = db.Appointments.AsNoTracking().Where(x => x.StartsAt >= start && x.StartsAt < end);
        var paid = db.Invoices.AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Paid && x.PaidAtUtc.HasValue &&
                x.PaidAtUtc.Value >= start && x.PaidAtUtc.Value < end);
        var byMonth = await paid.GroupBy(x => x.PaidAtUtc!.Value.Month)
            .Select(g => new { Month = g.Key, Count = g.Count(), Total = g.Sum(x => x.Total) })
            .ToListAsync();
        return new ReportsVm
        {
            Year = year,
            Appointments = await appts.CountAsync(),
            Completed = await appts.CountAsync(x => x.Status == AppointmentStatus.Completed),
            Canceled = await appts.CountAsync(x => x.Status == AppointmentStatus.Canceled),
            Revenue = byMonth.Sum(x => x.Total),
            Months = Enumerable.Range(1, 12)
                .Select(m => new MonthlyRevenueVm
                {
                    Year = year,
                    Month = m,
                    Total = byMonth.FirstOrDefault(x => x.Month == m)?.Total ?? 0m,
                    InvoiceCount = byMonth.FirstOrDefault(x => x.Month == m)?.Count ?? 0
                }).ToList()
        };
    }
}
