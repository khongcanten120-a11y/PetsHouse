namespace PetsHouse.Models;

public class DashboardVm
{
    public int Customers { get; set; }
    public int Pets { get; set; }
    public int Appointments { get; set; }
    public int Waiting { get; set; }
    public int Doctors { get; set; }
    public decimal Revenue { get; set; }
    public int LowStock { get; set; }

    public DateTime SelectedDay { get; set; } = DateTime.Today;
    public int TodayAppointments { get; set; }
    public int InProgressToday { get; set; }
    public int WaitingToday { get; set; }
    public int CompletedToday { get; set; }
    public List<Appointment> RecentAppointments { get; set; } = new();
    public List<Appointment> TodaySchedule { get; set; } = new();
    public List<DailyAppointmentPoint> WeeklyVisits { get; set; } = new();
}

public class DailyAppointmentPoint
{
    public DateTime Day { get; set; }
    public int Count { get; set; }
}

public class StockFormVm
{
    public int MedicineId { get; set; }
    public string? Name { get; set; }
    public int Quantity { get; set; }
}
