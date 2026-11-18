namespace PetsHouse.Models;

// Cùng một danh mục hai ca được sử dụng ở Doctor, Staff và Admin.
public sealed class ShiftTemplate
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
}

public static class ShiftCatalog
{
    public static readonly IReadOnlyList<ShiftTemplate> All = new List<ShiftTemplate>
    {
        new()
        {
            Code = "CA1",
            Name = "Ca 1",
            Start = new TimeSpan(8, 0, 0),
            End = new TimeSpan(16, 0, 0)
        },
        new()
        {
            Code = "CA2",
            Name = "Ca 2",
            Start = new TimeSpan(16, 0, 0),
            End = new TimeSpan(20, 0, 0)
        }
    };

    public static ShiftTemplate? Find(string? code) =>
        All.FirstOrDefault(x => string.Equals(
            x.Code, code, StringComparison.OrdinalIgnoreCase));

    public static bool TryGetWindow(
        DateTime workDate,
        string? shiftCode,
        out DateTime startAt,
        out DateTime endAt)
    {
        startAt = default;
        endAt = default;

        if (workDate == default || workDate.Year < 2020 || workDate.Year > 2100)
        {
            return false;
        }

        var template = Find(shiftCode);
        if (template is null)
        {
            return false;
        }

        startAt = workDate.Date.Add(template.Start);
        endAt = workDate.Date.Add(template.End);
        return true;
    }

    public static bool IsStandard(DateTime startAt, DateTime endAt) =>
        All.Any(x =>
            startAt.TimeOfDay == x.Start &&
            endAt == startAt.Date.Add(x.End));

    public static string? ValidateFuture(DateTime startAt, DateTime endAt)
    {
        if (!IsStandard(startAt, endAt))
        {
            return "Chỉ được đăng ký Ca 1 (08:00–16:00) hoặc Ca 2 (16:00–20:00).";
        }

        if (startAt <= DateTime.Now)
        {
            return "Ca làm việc phải bắt đầu trong tương lai.";
        }

        if (startAt > DateTime.Now.AddDays(180))
        {
            return "Chỉ được xếp lịch tối đa 180 ngày tới.";
        }

        return null;
    }
}
