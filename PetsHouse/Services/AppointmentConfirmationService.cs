using System.Data;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Services;

/// <summary>
/// Admin và Staff dùng chung truy vấn hàng chờ và nghiệp vụ xác nhận.
/// Không thêm bảng hoặc sửa cấu trúc cơ sở dữ liệu.
/// </summary>
public static class AppointmentConfirmationService
{
    private static IQueryable<Appointment> PendingQuery(AppDbContext db)
    {
        return db.Appointments.AsNoTracking().Where(appointment =>
            appointment.Status == AppointmentStatus.Pending ||
            appointment.Status == AppointmentStatus.Confirmed);
    }

    public static async Task FillPendingAsync(
        AppDbContext db,
        AdminAppointmentListVm vm)
    {
        var pending = PendingQuery(db);

        vm.PendingTotal = await pending.CountAsync();
        vm.PendingAppointments = await pending
            .Include(x => x.Customer)
            .Include(x => x.Pet)
            .Include(x => x.ClinicService)
            .Include(x => x.Veterinarian)
            .OrderBy(x => x.StartsAt)
            .ThenBy(x => x.Id)
            .Take(100)
            .ToListAsync();

        if (vm.PendingAppointments.Count == 0)
        {
            return;
        }

        var earliest = vm.PendingAppointments.Min(x => x.StartsAt);
        var latest = vm.PendingAppointments.Max(x => x.EndsAt);

        // Chỉ bác sĩ đang hoạt động, có tài khoản đăng nhập được.
        var doctors = await db.Veterinarians.AsNoTracking()
            .Where(x => x.IsActive && x.User.PasswordHash != null)
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var shifts = await db.WorkShifts.AsNoTracking()
            .Where(x => x.StartAt < latest && x.EndAt > earliest)
            .ToListAsync();

        var busyAppointments = await db.Appointments.AsNoTracking()
            .Where(x => x.VeterinarianId != null &&
                        x.Status != AppointmentStatus.Canceled &&
                        x.Status != AppointmentStatus.Completed &&
                        x.StartsAt < latest &&
                        x.EndsAt > earliest)
            .Select(x => new
            {
                x.Id,
                x.VeterinarianId,
                x.StartsAt,
                x.EndsAt
            })
            .ToListAsync();

        foreach (var appointment in vm.PendingAppointments)
        {
            vm.EligibleDoctors[appointment.Id] = doctors
                .Where(doctor =>
                    shifts.Any(shift =>
                        shift.VeterinarianId == doctor.Id &&
                        shift.StartAt <= appointment.StartsAt &&
                        shift.EndAt >= appointment.EndsAt) &&
                    !busyAppointments.Any(busy =>
                        busy.Id != appointment.Id &&
                        busy.VeterinarianId == doctor.Id &&
                        busy.StartsAt < appointment.EndsAt &&
                        busy.EndsAt > appointment.StartsAt))
                .ToList();
        }
    }

    public static async Task<PendingSnapshot> ReadPendingSnapshotAsync(AppDbContext db)
    {
        var pending = PendingQuery(db);
        return new PendingSnapshot
        {
            Count = await pending.CountAsync(),
            Ids = await pending
                .OrderBy(x => x.StartsAt)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .Take(100)
                .ToListAsync()
        };
    }

    public static async Task<(bool Ok, string Message)> ConfirmAsync(
        AppDbContext db,
        int appointmentId,
        int doctorId)
    {
        if (appointmentId <= 0 || doctorId <= 0)
        {
            return (false, "Vui lòng chọn lịch hẹn và bác sĩ hợp lệ.");
        }

        // Khóa bản ghi lịch hẹn, ngăn Admin và Staff cùng xác nhận một lúc.
        // Sau đó khóa bản ghi bác sĩ, ngăn hai lịch trùng giờ được xác nhận song song.
        await using var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var appointment = await db.Appointments
            .FromSqlInterpolated($"SELECT * FROM dbo.Appointments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {appointmentId}")
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (appointment is null)
        {
            return (false, "Lịch hẹn không tồn tại.");
        }

        if (appointment.Status != AppointmentStatus.Pending &&
            appointment.Status != AppointmentStatus.Confirmed)
        {
            return (false, "Lịch này đã được xác nhận, hủy hoặc xử lý bởi tài khoản khác. Hãy tải lại trang.");
        }

        if (appointment.EndsAt <= DateTime.Now)
        {
            return (false, "Lịch đã qua giờ khám, không thể xác nhận. Vui lòng điều chỉnh lịch.");
        }

        var doctor = await db.Veterinarians
            .FromSqlInterpolated($"SELECT * FROM dbo.Veterinarians WITH (UPDLOCK, HOLDLOCK) WHERE Id = {doctorId}")
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (doctor is null || !doctor.IsActive)
        {
            return (false, "Bác sĩ không tồn tại hoặc đã ngừng hoạt động.");
        }

        var doctorCanLogin = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == doctor.UserId && x.PasswordHash != null);

        if (!doctorCanLogin)
        {
            return (false, "Bác sĩ này chưa có tài khoản đăng nhập hợp lệ để nhận ca khám.");
        }

        var hasShift = await db.WorkShifts.AsNoTracking().AnyAsync(x =>
            x.VeterinarianId == doctorId &&
            x.StartAt <= appointment.StartsAt &&
            x.EndAt >= appointment.EndsAt);

        if (!hasShift)
        {
            return (false, "Bác sĩ chưa được xếp ca 08:00–16:00 hoặc 16:00–20:00 phù hợp giờ khám.");
        }

        var overlaps = await db.Appointments.AsNoTracking().AnyAsync(x =>
            x.Id != appointmentId &&
            x.VeterinarianId == doctorId &&
            x.Status != AppointmentStatus.Canceled &&
            x.Status != AppointmentStatus.Completed &&
            x.StartsAt < appointment.EndsAt &&
            x.EndsAt > appointment.StartsAt);

        if (overlaps)
        {
            return (false, "Bác sĩ đã có lịch khám trùng giờ. Vui lòng chọn bác sĩ khác.");
        }

        // Điều kiện WHERE theo trạng thái bảo vệ ngay cả khi có thao tác đồng thời.
        var updatedRows = await db.Appointments
            .Where(x => x.Id == appointmentId &&
                        (x.Status == AppointmentStatus.Pending ||
                         x.Status == AppointmentStatus.Confirmed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.VeterinarianId, (int?)doctorId)
                .SetProperty(x => x.Status, AppointmentStatus.Waiting));

        if (updatedRows != 1)
        {
            return (false, "Lịch hẹn đã được xử lý trước đó, vui lòng tải lại trang.");
        }

        await transaction.CommitAsync();

        return (true,
            $"Đã xác nhận lịch #{appointmentId}, chuyển sang Chờ khám cho {doctor.FullName}.");
    }
}

public sealed class PendingSnapshot
{
    public int Count { get; set; }
    public List<int> Ids { get; set; } = new();
}
