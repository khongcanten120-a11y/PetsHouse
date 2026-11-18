namespace PetsHouse.Models;

public static class ShiftCalendarFactory
{
    public static DateTime GetGridStart(int year, int month)
    {
        var first = new DateTime(year, month, 1);
        var distanceFromMonday = ((int)first.DayOfWeek + 6) % 7;
        return first.AddDays(-distanceFromMonday);
    }

    public static ShiftCalendarVm Build(
        int year,
        int month,
        IReadOnlyList<WorkShift> shifts,
        List<Veterinarian> doctors,
        IReadOnlyList<DoctorShiftRequest>? requests = null)
    {
        requests ??= Array.Empty<DoctorShiftRequest>();

        var vm = new ShiftCalendarVm
        {
            Year = year,
            Month = month,
            Doctors = doctors
        };

        var gridStart = GetGridStart(year, month);
        var now = DateTime.Now;

        // Giữ 42 ô (6 tuần), tạo đúng hai ca/ngày.
        for (var index = 0; index < 42; index++)
        {
            var date = gridStart.AddDays(index);
            var day = new ShiftCalendarDayVm
            {
                Date = date,
                IsCurrentMonth = date.Month == month && date.Year == year,
                IsToday = date.Date == DateTime.Today
            };

            foreach (var template in ShiftCatalog.All)
            {
                var start = date.Add(template.Start);
                var end = date.Add(template.End);

                var overlapping = shifts
                    .Where(x => x.StartAt < end && x.EndAt > start)
                    .ToList();

                var covering = overlapping
                    .Where(x => x.StartAt <= start && x.EndAt >= end)
                    .GroupBy(x => x.VeterinarianId)
                    .Select(group => group.OrderBy(x => x.Id).First())
                    .OrderBy(x => x.Veterinarian.FullName)
                    .ToList();

                var doctorConflict = overlapping
                    .GroupBy(x => x.VeterinarianId)
                    .Any(group =>
                    {
                        var ordered = group.OrderBy(x => x.StartAt).ToList();
                        for (var i = 1; i < ordered.Count; i++)
                        {
                            if (ordered[i - 1].EndAt > ordered[i].StartAt)
                            {
                                return true;
                            }
                        }
                        return false;
                    });

                var registered = requests
                    .Where(x => x.StartAt == start && x.EndAt == end)
                    .OrderBy(x => x.Veterinarian.FullName)
                    .Select(x => new ShiftRequestEntryVm
                    {
                        RequestId = x.Id,
                        DoctorId = x.VeterinarianId,
                        DoctorName = x.Veterinarian.FullName,
                        Status = x.Status
                    }).ToList();

                var slot = new ShiftCalendarSlotVm
                {
                    Code = template.Code,
                    Name = template.Name,
                    TimeLabel = $"{template.Start.Hours:00}:{template.Start.Minutes:00}–{template.End.Hours:00}:{template.End.Minutes:00}",
                    StartAt = start,
                    EndAt = end,
                    HasNonstandardOverlap = overlapping.Any(x =>
                        x.StartAt != start || x.EndAt != end),
                    HasDoctorConflict = doctorConflict,
                    Requests = registered,
                    Assignments = covering.Select(x => new ShiftCalendarEntryVm
                    {
                        ShiftId = x.Id,
                        DoctorId = x.VeterinarianId,
                        DoctorName = x.Veterinarian.FullName,
                        IsStandard = ShiftCatalog.IsStandard(x.StartAt, x.EndAt)
                    }).ToList()
                };

                foreach (var assigned in slot.Assignments)
                {
                    assigned.ReplacementCandidates = registered
                        .Where(x => x.DoctorId != assigned.DoctorId &&
                                    doctors.Any(d => d.Id == x.DoctorId && d.IsActive) &&
                                    (x.Status == ShiftRequestStatus.Accepted ||
                                     x.Status == ShiftRequestStatus.Assigned))
                        .ToList();
                }

                if (day.IsCurrentMonth)
                {
                    vm.PendingCount += registered.Count(x => x.Status == ShiftRequestStatus.Pending);
                    vm.AcceptedCount += registered.Count(x => x.Status == ShiftRequestStatus.Accepted);
                }

                day.Slots.Add(slot);

                if (day.IsCurrentMonth && slot.HasDoctorConflict)
                {
                    vm.ConflictCount++;
                }

                if (day.IsCurrentMonth && slot.Missing)
                {
                    vm.MissingCount++;
                    if (slot.StartAt > now)
                    {
                        vm.MissingUpcomingCount++;
                    }
                }
            }

            vm.Days.Add(day);
        }

        vm.LegacyShifts = shifts
            .Where(x => !ShiftCatalog.IsStandard(x.StartAt, x.EndAt))
            .OrderBy(x => x.StartAt)
            .Select(x => new ShiftCalendarLegacyVm
            {
                ShiftId = x.Id,
                DoctorName = x.Veterinarian.FullName,
                StartAt = x.StartAt,
                EndAt = x.EndAt
            })
            .ToList();

        return vm;
    }
}
