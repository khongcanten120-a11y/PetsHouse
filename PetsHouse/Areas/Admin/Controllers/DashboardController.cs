
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class DashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(DateTime? day = null)
    {
        // Xác định ngày được chọn
        var selectedDay = day?.Date ?? DateTime.Today;

        var endOfDay = selectedDay.AddDays(1);

        var weekStart = selectedDay.AddDays(-6);

        // Lấy lịch hẹn trong ngày
        var todayQuery = db.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.StartsAt >= selectedDay &&
                appointment.StartsAt < endOfDay);

        // Lấy lịch hẹn trong 7 ngày
        var weekAppointments = await db.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.StartsAt >= weekStart &&
                appointment.StartsAt < endOfDay)
            .Select(appointment => appointment.StartsAt)
            .ToListAsync();

        // Thống kê tổng quan hệ thống
        var viewModel = new DashboardVm
        {
            SelectedDay = selectedDay,

            // Tổng số khách hàng
            Customers = await db.Customers.CountAsync(),

            // Tổng số thú cưng
            Pets = await db.Pets.CountAsync(),

            // Tổng số bác sĩ
            Doctors = await db.Veterinarians.CountAsync(),

            // Tổng số lịch hẹn
            Appointments = await db.Appointments.CountAsync(),

            // Số lịch đang chờ xử lý
            Waiting = await db.Appointments.CountAsync(
                appointment =>
                    appointment.Status == AppointmentStatus.Waiting ||
                    appointment.Status == AppointmentStatus.Pending
            ),

            // Số loại thuốc sắp hết
            LowStock = await db.Medicines.CountAsync(
                medicine =>
                    medicine.IsActive &&
                    medicine.QuantityOnHand <= medicine.ReorderLevel
            ),

            // Tổng doanh thu đã thanh toán
            Revenue = await db.Invoices
                .Where(invoice =>
                    invoice.Status == PaymentStatus.Paid)
                .SumAsync(invoice => (decimal?)invoice.Total) ?? 0m,

            // Tổng lịch hẹn hôm nay
            TodayAppointments = await todayQuery.CountAsync(),

            // Số ca đang khám hôm nay
            InProgressToday = await todayQuery.CountAsync(
                appointment =>
                    appointment.Status == AppointmentStatus.InProgress
            ),

            // Số ca chờ khám hôm nay
            WaitingToday = await todayQuery.CountAsync(
                appointment =>
                    appointment.Status == AppointmentStatus.Pending ||
                    appointment.Status == AppointmentStatus.Confirmed ||
                    appointment.Status == AppointmentStatus.Waiting
            ),

            // Số ca đã hoàn thành hôm nay
            CompletedToday = await todayQuery.CountAsync(
                appointment =>
                    appointment.Status == AppointmentStatus.Completed
            ),

            // Lịch khám hôm nay - theo giờ tăng dần
            TodaySchedule = await todayQuery
                .Include(appointment => appointment.Pet)
                .Include(appointment => appointment.Customer)
                .Include(appointment => appointment.Veterinarian)
                .OrderBy(appointment => appointment.StartsAt)
                .ThenBy(appointment => appointment.Id)
                .Take(5)
                .ToListAsync(),

            // Danh sách lịch hẹn - ID tăng dần
            RecentAppointments = await db.Appointments
                .AsNoTracking()
                .Include(appointment => appointment.Customer)
                .Include(appointment => appointment.Pet)
                .Include(appointment => appointment.Veterinarian)
                .Include(appointment => appointment.ClinicService)
                .OrderBy(appointment => appointment.Id)
                .Take(8)
                .ToListAsync()
        };

        // Biểu đồ số lượt khám trong 7 ngày
        viewModel.WeeklyVisits = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = weekStart.AddDays(offset);

                return new DailyAppointmentPoint
                {
                    Day = date,

                    Count = weekAppointments.Count(
                        appointment =>
                            appointment.Date == date
                    )
                };
            })
            .ToList();

        return View(viewModel);
    }
}
