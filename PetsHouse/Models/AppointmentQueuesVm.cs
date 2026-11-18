using PetsHouse.Models;

namespace PetsHouse.Models;

// Dữ liệu riêng của trang quản trị lịch hẹn.
// Không thay đổi các entity hoặc database hiện có.
public sealed class AdminAppointmentListVm
{
    public List<Appointment> PendingAppointments { get; set; } = new();
    public Dictionary<int, List<Veterinarian>> EligibleDoctors { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();

    public AppointmentStatus? Status { get; set; }
    public DateTime? Day { get; set; }
    public string Query { get; set; } = "";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int Total { get; set; }
    public int PendingTotal { get; set; }

    public int TotalPages => Math.Max(1,
        (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed class DoctorQueueVm
{
    public string DoctorName { get; set; } = "";
    public List<Appointment> WaitingAppointments { get; set; } = new();
    public List<Appointment> OtherAppointments { get; set; } = new();
}
