using System.ComponentModel.DataAnnotations;

namespace PetsHouse.Models;

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
}

public class RegisterVm
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, Phone] public string Phone { get; set; } = "";
    [Required, MinLength(8), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}

public class PetVm
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, StringLength(60)] public string Species { get; set; } = "";
    [StringLength(80)] public string? Breed { get; set; }
    [StringLength(15)] public string? Gender { get; set; }
    [DataType(DataType.Date)] public DateTime? BirthDate { get; set; }
    [Range(0, 500)] public decimal? WeightKg { get; set; }
    [StringLength(300)] public string? Notes { get; set; }
}

public class AppointmentVm
{
    [Range(1, int.MaxValue)] public int PetId { get; set; }
    [Range(1, int.MaxValue)] public int ServiceId { get; set; }
    public int? VeterinarianId { get; set; }
    [Required] public DateTime StartsAt { get; set; }
    [StringLength(600)] public string? Notes { get; set; }
}

public class StaffCreateVm
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string TemporaryPassword { get; set; } = "";
    [Required] public string Role { get; set; } = Roles.Staff;
    public string? Specialty { get; set; }
}

public class ExamineVm
{
    public int AppointmentId { get; set; }
    [Required, StringLength(1500)] public string Symptoms { get; set; } = "";
    [Required, StringLength(1500)] public string Diagnosis { get; set; } = "";
    [StringLength(1500)] public string? Treatment { get; set; }
    // Một phiếu khám có thể kê nhiều loại thuốc; danh sách được Razor model binding.
    public List<PrescriptionLineVm> Items { get; set; } = new()
    {
        new PrescriptionLineVm()
    };
}

public class PrescriptionLineVm
{
    public int? MedicineId { get; set; }
    [Range(1, 9999)] public int? Quantity { get; set; }
    [StringLength(400)] public string? Directions { get; set; }
}

public class PaymentVm
{
    public int AppointmentId { get; set; }
    [Required] public string Method { get; set; } = "Tiền mặt";
    [Range(0, 1000000000)] public decimal AmountTendered { get; set; }
}

public class ReceiveVm
{
    [Range(1, int.MaxValue)] public int MedicineId { get; set; }
    [Range(1, 999999)] public int Quantity { get; set; }
}

public class ShiftVm
{
    [Range(1, int.MaxValue)] public int DoctorId { get; set; }
    [Required] public DateTime StartAt { get; set; }
    [Required] public DateTime EndAt { get; set; }
}
