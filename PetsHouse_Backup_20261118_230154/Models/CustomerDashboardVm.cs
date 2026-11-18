using System.ComponentModel.DataAnnotations;

namespace PetsHouse.Models;

public class CustomerDashboardVm
{
    public string CustomerName { get; set; } = "";
    public int PetCount { get; set; }
    public int AppointmentCount { get; set; }
    public int UpcomingCount { get; set; }
    public List<Appointment> RecentAppointments { get; set; } = new();
}

public class CustomerProfileVm
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(100)]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Nhập số điện thoại."), Phone, StringLength(30)]
    public string Phone { get; set; } = "";
    [StringLength(200)] public string? Address { get; set; }
}
