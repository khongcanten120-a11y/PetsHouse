using System.ComponentModel.DataAnnotations;

namespace PetsHouse.Models;

public class ShiftAssignmentForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn bác sĩ hợp lệ.")]
    public int DoctorId { get; set; }

    [Required(ErrorMessage = "Chọn ngày làm việc.")]
    [DataType(DataType.Date)]
    public DateTime WorkDate { get; set; }

    [Required(ErrorMessage = "Chọn ca làm việc.")]
    public string ShiftCode { get; set; } = "";
}

public class DoctorShiftForm
{
    [Required]
    [DataType(DataType.Date)]
    public DateTime WorkDate { get; set; }

    [Required]
    public string ShiftCode { get; set; } = "";
}

public class ShiftReplaceForm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Range(1, int.MaxValue)]
    public int NewDoctorId { get; set; }

    [Required, StringLength(400, MinimumLength = 3)]
    public string Reason { get; set; } = "";
}

public class ShiftMoveForm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime WorkDate { get; set; }

    [Required]
    public string ShiftCode { get; set; } = "";
}

public class ShiftCalendarVm
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool HasDoctorProfile { get; set; } = true;
    public string? CurrentDoctorName { get; set; }
    public List<Veterinarian> Doctors { get; set; } = new();
    public List<ShiftCalendarDayVm> Days { get; set; } = new();
    public List<ShiftCalendarLegacyVm> LegacyShifts { get; set; } = new();
    public int PendingCount { get; set; }
    public int AcceptedCount { get; set; }
    public int MissingCount { get; set; }
    public int MissingUpcomingCount { get; set; }
    public int ConflictCount { get; set; }
}

public class ShiftCalendarDayVm
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public List<ShiftCalendarSlotVm> Slots { get; set; } = new();
}

public class ShiftCalendarSlotVm
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TimeLabel { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool HasNonstandardOverlap { get; set; }
    public bool HasDoctorConflict { get; set; }
    public List<ShiftCalendarEntryVm> Assignments { get; set; } = new();
    public List<ShiftRequestEntryVm> Requests { get; set; } = new();
    public bool Missing => Assignments.Count == 0;
}

public class ShiftCalendarEntryVm
{
    public int ShiftId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public bool IsStandard { get; set; }
    public List<ShiftRequestEntryVm> ReplacementCandidates { get; set; } = new();
}

public class ShiftRequestEntryVm
{
    public int RequestId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public string Status { get; set; } = "";
}


public class ShiftCalendarLegacyVm
{
    public int ShiftId { get; set; }
    public string DoctorName { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}
