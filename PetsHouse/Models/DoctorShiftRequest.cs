using System.ComponentModel.DataAnnotations;

namespace PetsHouse.Models;

// Yêu cầu bác sĩ đăng ký - CHƯA PHẢI ca đã được phân công.
// Ca làm chính thức vẫn nằm ở bảng WorkShifts.
public static class ShiftRequestStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Assigned = "Assigned";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public const string Replaced = "Replaced";
    public const string Canceled = "Canceled";
}

public class DoctorShiftRequest
{
    public int Id { get; set; }
    public int VeterinarianId { get; set; }
    public Veterinarian Veterinarian { get; set; } = null!;

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = ShiftRequestStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    [MaxLength(400)]
    public string? ReviewNote { get; set; }
}

public class ShiftRequestDecisionForm
{
    [Range(1, int.MaxValue)]
    public int RequestId { get; set; }

    [MaxLength(400)]
    public string? Note { get; set; }
}

public class ShiftCancelWithReasonForm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do hủy ca.")]
    [StringLength(400, MinimumLength = 3)]
    public string Reason { get; set; } = "";
}
