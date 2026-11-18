using System.ComponentModel.DataAnnotations;

namespace PetsHouse.Models;

public class StaffCustomerCreateVm
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên khách hàng.")]
    [StringLength(100)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";

    [Required, Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(30)]
    public string Phone { get; set; } = "";

    [StringLength(200)]
    public string? Address { get; set; }

    [Required, MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    public string TemporaryPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên thú cưng.")]
    [StringLength(80)]
    public string PetName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập loài thú cưng.")]
    [StringLength(60)]
    public string Species { get; set; } = "";

    [StringLength(80)]
    public string? Breed { get; set; }

    [StringLength(15)]
    public string? Gender { get; set; }
}

public class StaffPetCreateVm
{
    [Range(1, int.MaxValue, ErrorMessage = "Khách hàng không hợp lệ.")]
    public int CustomerId { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = "";

    [Required, StringLength(60)]
    public string Species { get; set; } = "";

    [StringLength(80)]
    public string? Breed { get; set; }

    [StringLength(15)]
    public string? Gender { get; set; }

    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    [Range(0, 500)]
    public decimal? WeightKg { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }
}

public class StaffAppointmentCreateVm
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Range(1, int.MaxValue)]
    public int PetId { get; set; }

    [Range(1, int.MaxValue)]
    public int ServiceId { get; set; }

    public int? VeterinarianId { get; set; }

    [Required]
    public DateTime StartsAt { get; set; } = DateTime.Today.AddDays(1).AddHours(9);

    [StringLength(600)]
    public string? Notes { get; set; }
}

public class StaffAppointmentEditVm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public DateTime StartsAt { get; set; }

    public int? VeterinarianId { get; set; }
}

public class StaffShiftCreateVm
{
    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }
}

public class StaffShiftEditVm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }
}

public class StaffShiftReplaceVm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Range(1, int.MaxValue)]
    public int NewDoctorId { get; set; }
}
