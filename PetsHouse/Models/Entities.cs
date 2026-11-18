using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PetsHouse.Models;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Staff = "Staff";
    public const string Doctor = "Doctor";
    public const string Warehouse = "Warehouse";
    public const string Admin = "Admin";
    public const string Employees = "Admin,Staff,Doctor,Warehouse";
}

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)] public string FullName { get; set; } = "";
}

public class Customer
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(200)] public string? Address { get; set; }
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
}

public class Pet
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [Required, MaxLength(60)] public string Species { get; set; } = "";
    [MaxLength(80)] public string? Breed { get; set; }
    [MaxLength(15)] public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    [Range(0, 500)] public decimal? WeightKg { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}

public class Veterinarian
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    [MaxLength(100)] public string Specialty { get; set; } = "Thú y tổng quát";
    public bool IsActive { get; set; } = true;
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
}

public class WorkShift
{
    public int Id { get; set; }
    public int VeterinarianId { get; set; }
    public Veterinarian Veterinarian { get; set; } = null!;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}

public class ClinicService
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    [Range(0, 1000000000)] public decimal Price { get; set; }
    [Range(15, 480)] public int DurationMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

public enum AppointmentStatus
{
    Pending, Confirmed, Waiting, InProgress, AwaitingPayment, Completed, Canceled
}

public class Appointment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;
    public int ClinicServiceId { get; set; }
    public ClinicService ClinicService { get; set; } = null!;
    public int? VeterinarianId { get; set; }
    public Veterinarian? Veterinarian { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    [MaxLength(600)] public string? Notes { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public MedicalRecord? MedicalRecord { get; set; }
    public Invoice? Invoice { get; set; }
}

public class MedicalRecord
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    [Required, MaxLength(1500)] public string Symptoms { get; set; } = "";
    [Required, MaxLength(1500)] public string Diagnosis { get; set; } = "";
    [MaxLength(1500)] public string? Treatment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<PrescriptionItem> Prescriptions { get; set; } = new List<PrescriptionItem>();
}

public class Medicine
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(30)] public string Unit { get; set; } = "Đơn vị";
    [Range(0, 1000000000)] public decimal UnitPrice { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; } = 10;
    public DateTime? ExpiresOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PrescriptionItem
{
    public int Id { get; set; }
    public int MedicalRecordId { get; set; }
    public MedicalRecord MedicalRecord { get; set; } = null!;
    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;
    public int Quantity { get; set; }
    [MaxLength(400)] public string Directions { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public DateTime? DispensedAtUtc { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;
    public int QuantityChange { get; set; }
    [Required, MaxLength(150)] public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Vaccination
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;
    public int VeterinarianId { get; set; }
    public Veterinarian Veterinarian { get; set; } = null!;
    [Required, MaxLength(100)] public string VaccineName { get; set; } = "";
    public DateTime DateGiven { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? Notes { get; set; }
}

public enum PaymentStatus { Pending, Paid }

public class Invoice
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public decimal Total { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    [MaxLength(30)] public string? PaymentMethod { get; set; }
    public decimal AmountTendered { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    [Required, MaxLength(120)] public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
