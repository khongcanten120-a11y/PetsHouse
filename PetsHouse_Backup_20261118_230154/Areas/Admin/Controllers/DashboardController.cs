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
        var selectedDay = day?.Date ?? DateTime.Today;
        var endOfDay = selectedDay.AddDays(1);
        var weekStart = selectedDay.AddDays(-6);

        var todayQuery = db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.StartsAt >= selectedDay &&
                                  appointment.StartsAt < endOfDay);

        var weekAppointments = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.StartsAt >= weekStart &&
                                  appointment.StartsAt < endOfDay)
            .Select(appointment => appointment.StartsAt)
            .ToListAsync();

        var viewModel = new DashboardVm
        {
            SelectedDay = selectedDay,
            Customers = await db.Customers.CountAsync(),
            Pets = await db.Pets.CountAsync(),
            Doctors = await db.Veterinarians.CountAsync(),
            Appointments = await db.Appointments.CountAsync(),
            Waiting = await db.Appointments.CountAsync(appointment =>
                appointment.Status == AppointmentStatus.Waiting ||
                appointment.Status == AppointmentStatus.Pending),
            LowStock = await db.Medicines.CountAsync(medicine =>
                medicine.IsActive && medicine.QuantityOnHand <= medicine.ReorderLevel),
            Revenue = await db.Invoices
                .Where(invoice => invoice.Status == PaymentStatus.Paid)
                .SumAsync(invoice => (decimal?)invoice.Total) ?? 0m,
            TodayAppointments = await todayQuery.CountAsync(),
            InProgressToday = await todayQuery.CountAsync(appointment =>
                appointment.Status == AppointmentStatus.InProgress),
            WaitingToday = await todayQuery.CountAsync(appointment =>
                appointment.Status == AppointmentStatus.Pending ||
                appointment.Status == AppointmentStatus.Confirmed ||
                appointment.Status == AppointmentStatus.Waiting),
            CompletedToday = await todayQuery.CountAsync(appointment =>
                appointment.Status == AppointmentStatus.Completed),
            TodaySchedule = await todayQuery
                .Include(appointment => appointment.Pet)
                .Include(appointment => appointment.Customer)
                .Include(appointment => appointment.Veterinarian)
                .OrderBy(appointment => appointment.StartsAt)
                .Take(5)
                .ToListAsync(),
            RecentAppointments = await db.Appointments
                .AsNoTracking()
                .Include(appointment => appointment.Customer)
                .Include(appointment => appointment.Pet)
                .Include(appointment => appointment.Veterinarian)
                .Include(appointment => appointment.ClinicService)
                .OrderByDescending(appointment => appointment.StartsAt)
                .Take(8)
                .ToListAsync()
        };

        viewModel.WeeklyVisits = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = weekStart.AddDays(offset);
                return new DailyAppointmentPoint
                {
                    Day = date,
                    Count = weekAppointments.Count(appointment => appointment.Date == date)
                };
            })
            .ToList();

        return View(viewModel);
    }
}
