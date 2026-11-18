using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = Roles.Staff + "," + Roles.Admin)]
public class WorkShiftsController(
    AppDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    // Giới hạn nhân sự cho MỖI ngày/ca, không phải số lượt đăng ký.
    private const int MaxDoctorsPerShift = 3;

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        if (y < 2020 || y > 2100 || m < 1 || m > 12)
        {
            return RedirectToAction(nameof(Index));
        }

        var from = ShiftCalendarFactory.GetGridStart(y, m);
        var to = from.AddDays(42);

        var doctors = await db.Veterinarians.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var shifts = await db.WorkShifts.AsNoTracking()
            .Include(x => x.Veterinarian)
            .Where(x => x.StartAt < to && x.EndAt > from)
            .OrderBy(x => x.StartAt)
            .ToListAsync();

        var requests = await db.DoctorShiftRequests.AsNoTracking()
            .Include(x => x.Veterinarian)
            .Where(x => x.StartAt >= from && x.StartAt < to)
            .ToListAsync();

        return View(ShiftCalendarFactory.Build(y, m, shifts, doctors, requests));
    }

    // Tiếp nhận yêu cầu: Pending -> Accepted. CHƯA xếp ca tại bước này.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(ShiftRequestDecisionForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Yêu cầu đăng ký không hợp lệ.";
            return Back();
        }

        var request = await db.DoctorShiftRequests
            .Include(x => x.Veterinarian)
            .FirstOrDefaultAsync(x => x.Id == form.RequestId);

        if (request is null) return NotFound();
        if (request.Status != ShiftRequestStatus.Pending ||
            ShiftCatalog.ValidateFuture(request.StartAt, request.EndAt) is not null ||
            !request.Veterinarian.IsActive)
        {
            TempData["Error"] = "Yêu cầu đã xử lý, quá hạn hoặc bác sĩ ngừng hoạt động.";
            return Back(request.StartAt);
        }

        var acceptedAt = DateTime.UtcNow;
        var reviewerId = users.GetUserId(User);
        var note = form.Note?.Trim();
        var count = await db.DoctorShiftRequests
            .Where(x => x.Id == request.Id && x.Status == ShiftRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ShiftRequestStatus.Accepted)
                .SetProperty(x => x.ReviewNote, note)
                .SetProperty(x => x.ReviewedAtUtc, acceptedAt)
                .SetProperty(x => x.ReviewedByUserId, reviewerId));

        if (count != 1)
        {
            TempData["Error"] = "Yêu cầu vừa được xử lý bởi người khác. Tải lại lịch.";
            return Back(request.StartAt);
        }

        TempData["Success"] = "Đã tiếp nhận. Nhấn 'Phân công' để xác lập ca làm việc.";
        return Back(request.StartAt);
    }

    // Phân công từ nút trong bảng yêu cầu: bắt buộc bác sĩ đã đăng ký và được tiếp nhận.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(ShiftRequestDecisionForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng chọn yêu cầu đăng ký hợp lệ.";
            return Back();
        }

        return await AssignAcceptedRequestAsync(form.RequestId);
    }

    // Tương thích với form 'Xếp ca' của các phiên bản cũ.
    // Không được tạo WorkShift trực tiếp: phải có đăng ký đúng ngày/ca đã tiếp nhận.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShiftAssignmentForm form)
    {
        if (!ModelState.IsValid ||
            !ShiftCatalog.TryGetWindow(
                form.WorkDate, form.ShiftCode, out var startAt, out var endAt))
        {
            TempData["Error"] = "Bác sĩ, ngày hoặc ca làm việc không hợp lệ.";
            return Back(form.WorkDate);
        }

        var validationError = ShiftCatalog.ValidateFuture(startAt, endAt);
        if (validationError is not null)
        {
            TempData["Error"] = validationError;
            return Back(startAt);
        }

        // Chỉ lấy đăng ký của ĐÚNG bác sĩ, ĐÚNG ngày, ĐÚNG ca và đã tiếp nhận.
        var requestId = await db.DoctorShiftRequests
            .AsNoTracking()
            .Where(request =>
                request.VeterinarianId == form.DoctorId &&
                request.StartAt == startAt &&
                request.EndAt == endAt &&
                request.Status == ShiftRequestStatus.Accepted &&
                request.Veterinarian.IsActive)
            .Select(request => (int?)request.Id)
            .FirstOrDefaultAsync();

        if (!requestId.HasValue)
        {
            TempData["Error"] =
                "Bác sĩ chưa đăng ký hoặc chưa được tiếp nhận đúng ngày và ca này. " +
                "Hãy để bác sĩ đăng ký trước.";
            return Back(startAt);
        }

        return await AssignAcceptedRequestAsync(requestId.Value);
    }

    private async Task<IActionResult> AssignAcceptedRequestAsync(int requestId)
    {
        // Serializable giúp giảm xung đột khi hai nhân viên phân công cùng lúc.
        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var request = await db.DoctorShiftRequests
            .AsNoTracking()
            .Include(x => x.Veterinarian)
            .FirstOrDefaultAsync(x => x.Id == requestId);

        if (request is null)
        {
            TempData["Error"] = "Không tìm thấy yêu cầu đăng ký ca của bác sĩ.";
            return Back();
        }

        // Tuyệt đối không phân công nếu chưa có yêu cầu đã được tiếp nhận.
        if (request.Status != ShiftRequestStatus.Accepted)
        {
            TempData["Error"] =
                "Chỉ được xếp lịch cho bác sĩ đã đăng ký và được tiếp nhận ca này.";
            return Back(request.StartAt);
        }

        if (!request.Veterinarian.IsActive)
        {
            TempData["Error"] = "Bác sĩ đã ngừng hoạt động.";
            return Back(request.StartAt);
        }

        var validationError =
            ShiftCatalog.ValidateFuture(request.StartAt, request.EndAt);

        if (validationError is not null)
        {
            TempData["Error"] = validationError;
            return Back(request.StartAt);
        }

        var hasOverlappingShift = await db.WorkShifts.AnyAsync(shift =>
            shift.VeterinarianId == request.VeterinarianId &&
            shift.StartAt < request.EndAt &&
            shift.EndAt > request.StartAt);

        if (hasOverlappingShift)
        {
            TempData["Error"] =
                "Bác sĩ đã được phân công ca này hoặc có ca làm việc trùng giờ.";
            return Back(request.StartAt);
        }

        // Chỉ Staff hoặc Admin đến được action này (Authorize ở cấp Controller).
        // Yêu cầu được tiếp nhận CHƯA được coi là phân công.
        // Kiểm tra sức chứa dưới transaction Serializable để hai tài khoản
        // không thể cùng xếp vượt 3 bác sĩ cho một ca/ngày.
        var assignedCount = await db.WorkShifts
            .Where(shift => shift.StartAt < request.EndAt && shift.EndAt > request.StartAt)
            .Select(shift => shift.VeterinarianId)
            .Distinct()
            .CountAsync();

        if (assignedCount >= MaxDoctorsPerShift)
        {
            TempData["Error"] =
                "Ca đã đủ 3 bác sĩ được phân công. Đăng ký khác vẫn chờ xét duyệt, " +
                "không được tự chuyển thành lịch làm.";
            return Back(request.StartAt);
        }

        // Đổi trạng thái có điều kiện ngay tại SQL; không phân công lại yêu cầu
        // đã bị tài khoản khác xử lý.
        var updatedAt = DateTime.UtcNow;
        var reviewerId = users.GetUserId(User);

        var changed = await db.DoctorShiftRequests
            .Where(x => x.Id == requestId &&
                        x.Status == ShiftRequestStatus.Accepted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ShiftRequestStatus.Assigned)
                .SetProperty(x => x.ReviewedAtUtc, updatedAt)
                .SetProperty(x => x.ReviewedByUserId, reviewerId));

        if (changed != 1)
        {
            TempData["Error"] =
                "Đăng ký vừa được người khác xử lý. Hãy tải lại lịch làm việc.";
            return Back(request.StartAt);
        }

        db.WorkShifts.Add(new WorkShift
        {
            VeterinarianId = request.VeterinarianId,
            StartAt = request.StartAt,
            EndAt = request.EndAt
        });

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] =
            "Đã phân công bác sĩ theo đúng ngày/ca đã đăng ký và được tiếp nhận.";

        return Back(request.StartAt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ShiftRequestDecisionForm form)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(form.Note))
        {
            TempData["Error"] = "Vui lòng nhập lý do từ chối.";
            return Back();
        }

        var request = await db.DoctorShiftRequests
            .FirstOrDefaultAsync(x => x.Id == form.RequestId);
        if (request is null) return NotFound();

        if (request.Status != ShiftRequestStatus.Pending &&
            request.Status != ShiftRequestStatus.Accepted)
        {
            TempData["Error"] = "Không thể từ chối yêu cầu đã phân công/đã xử lý.";
            return Back(request.StartAt);
        }

        var rejectedAt = DateTime.UtcNow;
        var reviewerId = users.GetUserId(User);
        var reason = form.Note.Trim();
        var count = await db.DoctorShiftRequests
            .Where(x => x.Id == request.Id &&
                        (x.Status == ShiftRequestStatus.Pending ||
                         x.Status == ShiftRequestStatus.Accepted))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, ShiftRequestStatus.Rejected)
                .SetProperty(x => x.ReviewNote, reason)
                .SetProperty(x => x.ReviewedAtUtc, rejectedAt)
                .SetProperty(x => x.ReviewedByUserId, reviewerId));

        if (count != 1)
        {
            TempData["Error"] = "Yêu cầu vừa được xử lý bởi người khác.";
            return Back(request.StartAt);
        }

        TempData["Success"] = "Đã từ chối yêu cầu đăng ký ca.";
        return Back(request.StartAt);
    }

    // Thay bác sĩ: chỉ cho phép người ĐÃ ĐĂNG KÝ, ĐÃ ĐƯỢC TIẾP NHẬN đúng ngày/ca.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(ShiftReplaceForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Chọn bác sĩ thay thế và nhập lý do (tối thiểu 3 ký tự).";
            return Back();
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var oldShift = await db.WorkShifts.FirstOrDefaultAsync(x => x.Id == form.Id);
        if (oldShift is null) return NotFound();

        if (oldShift.StartAt <= DateTime.Now ||
            !ShiftCatalog.IsStandard(oldShift.StartAt, oldShift.EndAt))
        {
            TempData["Error"] = "Chỉ được thay bác sĩ ở ca chuẩn chưa bắt đầu.";
            return Back(oldShift.StartAt);
        }

        if (oldShift.VeterinarianId == form.NewDoctorId)
        {
            TempData["Error"] = "Không thể thay một bác sĩ bằng chính người đó.";
            return Back(oldShift.StartAt);
        }

        var registered = await db.DoctorShiftRequests
            .Include(x => x.Veterinarian)
            .FirstOrDefaultAsync(x =>
                x.VeterinarianId == form.NewDoctorId &&
                x.StartAt == oldShift.StartAt && x.EndAt == oldShift.EndAt &&
                (x.Status == ShiftRequestStatus.Accepted ||
                 x.Status == ShiftRequestStatus.Assigned));

        if (registered is null || !registered.Veterinarian.IsActive)
        {
            TempData["Error"] =
                "Chỉ chọn bác sĩ đã đăng ký và được tiếp nhận đúng ngày/ca này.";
            return Back(oldShift.StartAt);
        }

        var appointments = await db.Appointments
            .Where(x => x.VeterinarianId == oldShift.VeterinarianId &&
                        x.Status != AppointmentStatus.Canceled &&
                        x.StartsAt < oldShift.EndAt && x.EndsAt > oldShift.StartAt)
            .OrderBy(x => x.StartsAt)
            .ToListAsync();

        if (appointments.Any(x =>
            x.Status != AppointmentStatus.Pending &&
            x.Status != AppointmentStatus.Confirmed &&
            x.Status != AppointmentStatus.Waiting))
        {
            TempData["Error"] =
                "Ca có hồ sơ đang khám, chờ thanh toán hoặc đã hoàn thành. Không thể đổi bác sĩ.";
            return Back(oldShift.StartAt);
        }

        if (appointments.Any(x => x.StartsAt < oldShift.StartAt || x.EndsAt > oldShift.EndAt))
        {
            TempData["Error"] = "Có lịch khám nằm ngoài ca. Hãy điều chỉnh lịch khám trước.";
            return Back(oldShift.StartAt);
        }

        for (var i = 1; i < appointments.Count; i++)
        {
            if (appointments[i - 1].EndsAt > appointments[i].StartsAt)
            {
                TempData["Error"] = "Các lịch khám của bác sĩ cũ bị trùng. Hãy xử lý trước.";
                return Back(oldShift.StartAt);
            }
        }

        var replacementShifts = await db.WorkShifts
            .Where(x => x.VeterinarianId == form.NewDoctorId &&
                        x.StartAt < oldShift.EndAt && x.EndAt > oldShift.StartAt)
            .ToListAsync();

        if (replacementShifts.Any(x =>
            x.StartAt != oldShift.StartAt || x.EndAt != oldShift.EndAt))
        {
            TempData["Error"] = "Bác sĩ thay thế có lịch làm chồng giờ không đúng chuẩn.";
            return Back(oldShift.StartAt);
        }

        var targetShiftExists = replacementShifts.Any(x =>
            x.StartAt == oldShift.StartAt && x.EndAt == oldShift.EndAt);

        if (targetShiftExists)
        {
            TempData["Error"] =
                "Bác sĩ thay thế đã có tên trong ca này. Hãy chọn người khác để đổi.";
            return Back(oldShift.StartAt);
        }

        // Đổi 1 người lấy 1 người; không tăng số bác sĩ của ca.
        foreach (var appointment in appointments)
        {
            var busy = await db.Appointments.AnyAsync(x =>
                x.Id != appointment.Id &&
                x.VeterinarianId == form.NewDoctorId &&
                x.Status != AppointmentStatus.Canceled &&
                x.StartsAt < appointment.EndsAt && x.EndsAt > appointment.StartsAt);
            if (busy)
            {
                TempData["Error"] = $"Bác sĩ mới bận vào thời gian khám #{appointment.Id}.";
                return Back(oldShift.StartAt);
            }
        }

        if (!targetShiftExists)
        {
            db.WorkShifts.Add(new WorkShift
            {
                VeterinarianId = form.NewDoctorId,
                StartAt = oldShift.StartAt,
                EndAt = oldShift.EndAt
            });
        }

        foreach (var appointment in appointments)
        {
            appointment.VeterinarianId = form.NewDoctorId;
        }

        registered.Status = ShiftRequestStatus.Assigned;
        registered.ReviewedAtUtc = DateTime.UtcNow;
        registered.ReviewedByUserId = users.GetUserId(User);
        registered.ReviewNote = LimitNote("Được phân công thay thế: " + form.Reason.Trim());

        var oldRequest = await db.DoctorShiftRequests.FirstOrDefaultAsync(x =>
            x.VeterinarianId == oldShift.VeterinarianId &&
            x.StartAt == oldShift.StartAt && x.EndAt == oldShift.EndAt);
        if (oldRequest is not null)
        {
            oldRequest.Status = ShiftRequestStatus.Replaced;
            oldRequest.ReviewNote = LimitNote($"Thay bằng BS #{form.NewDoctorId}: {form.Reason.Trim()}");
            oldRequest.ReviewedAtUtc = DateTime.UtcNow;
            oldRequest.ReviewedByUserId = users.GetUserId(User);
        }

        db.WorkShifts.Remove(oldShift);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] =
            $"Đã thay bác sĩ và chuyển {appointments.Count} lịch khám chưa thực hiện.";
        return Back(oldShift.StartAt);
    }

    // Chuyển ngày/ca khi BS đã đăng ký và được tiếp nhận tại ngày/ca mới.
    // Không tự chuyển lịch hẹn của bệnh nhân.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(ShiftMoveForm form)
    {
        if (!ModelState.IsValid ||
            !ShiftCatalog.TryGetWindow(form.WorkDate, form.ShiftCode, out var start, out var end))
        {
            TempData["Error"] = "Ngày/ca mới không hợp lệ.";
            return Back(form.WorkDate);
        }

        var err = ShiftCatalog.ValidateFuture(start, end);
        if (err is not null)
        {
            TempData["Error"] = err;
            return Back(form.WorkDate);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var shift = await db.WorkShifts.FirstOrDefaultAsync(x => x.Id == form.Id);
        if (shift is null) return NotFound();

        if (shift.StartAt <= DateTime.Now ||
            !ShiftCatalog.IsStandard(shift.StartAt, shift.EndAt))
        {
            TempData["Error"] = "Chỉ chuyển ca chuẩn chưa bắt đầu.";
            return Back(shift.StartAt);
        }

        if (shift.StartAt == start && shift.EndAt == end)
        {
            TempData["Error"] = "Bạn đang chọn chính ca hiện tại.";
            return Back(shift.StartAt);
        }

        var hasAppts = await db.Appointments.AnyAsync(x =>
            x.VeterinarianId == shift.VeterinarianId &&
            x.Status != AppointmentStatus.Canceled &&
            x.StartsAt < shift.EndAt && x.EndsAt > shift.StartAt);
        if (hasAppts)
        {
            TempData["Error"] = "Ca có lịch khám. Hãy chuyển lịch khám trước.";
            return Back(shift.StartAt);
        }

        var requestedTarget = await db.DoctorShiftRequests.FirstOrDefaultAsync(x =>
            x.VeterinarianId == shift.VeterinarianId &&
            x.StartAt == start && x.EndAt == end &&
            (x.Status == ShiftRequestStatus.Accepted ||
             x.Status == ShiftRequestStatus.Assigned));
        if (requestedTarget is null)
        {
            TempData["Error"] = "Bác sĩ chưa đăng ký và được tiếp nhận ở ngày/ca mới.";
            return Back(shift.StartAt);
        }

        var overlapping = await db.WorkShifts.AnyAsync(x =>
            x.Id != shift.Id && x.VeterinarianId == shift.VeterinarianId &&
            x.StartAt < end && x.EndAt > start);
        if (overlapping)
        {
            TempData["Error"] = "Bác sĩ đã được phân công ở giờ mới.";
            return Back(shift.StartAt);
        }

        // Chuyển ca thực chất cũng chiếm thêm 1 vị trí tại ca đích.
        var targetCount = await db.WorkShifts
            .Where(x => x.Id != shift.Id && x.StartAt < end && x.EndAt > start)
            .Select(x => x.VeterinarianId)
            .Distinct()
            .CountAsync();
        if (targetCount >= MaxDoctorsPerShift)
        {
            TempData["Error"] = "Ca đích đã đủ 3 bác sĩ; không thể chuyển vào.";
            return Back(shift.StartAt);
        }

        var oldRequest = await db.DoctorShiftRequests.FirstOrDefaultAsync(x =>
            x.VeterinarianId == shift.VeterinarianId &&
            x.StartAt == shift.StartAt && x.EndAt == shift.EndAt);

        if (oldRequest is not null)
        {
            oldRequest.Status = ShiftRequestStatus.Canceled;
            oldRequest.ReviewNote = "Nhân viên chuyển sang ca khác.";
            oldRequest.ReviewedAtUtc = DateTime.UtcNow;
            oldRequest.ReviewedByUserId = users.GetUserId(User);
        }

        shift.StartAt = start;
        shift.EndAt = end;
        requestedTarget.Status = ShiftRequestStatus.Assigned;
        requestedTarget.ReviewNote = "Đã chuyển từ ca cũ.";
        requestedTarget.ReviewedAtUtc = DateTime.UtcNow;
        requestedTarget.ReviewedByUserId = users.GetUserId(User);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Success"] = "Đã chuyển ca theo đăng ký mới của bác sĩ.";
        return Back(start);
    }

    // Hủy MỘT phân công bác sĩ, không hủy toàn bộ ca/ngày.
    // Không yêu cầu bác sĩ khác có DoctorShiftRequest=Assigned vì các ca lịch sử
    // có thể được xếp trực tiếp trước khi tính năng đăng ký được triển khai.
    // Nếu đã có lịch khám, chỉ cho chuyển sang bác sĩ KHÁC ĐÃ CÓ LỊCH LÀM
    // bao trùm thời gian khám và chưa bận. Không tự động chuyển lịch khám.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        ShiftCancelWithReasonForm form,
        [FromForm] int? receiveDoctorId)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nhập lý do hủy ca (tối thiểu 3 ký tự).";
            return Back();
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var shift = await db.WorkShifts.FirstOrDefaultAsync(x => x.Id == form.Id);
        if (shift is null) return NotFound();

        if (shift.StartAt <= DateTime.Now)
        {
            TempData["Error"] = "Không được hủy phân công đã bắt đầu.";
            return Back(shift.StartAt);
        }

        // Lịch khám thuộc bác sĩ này và giao với ca muốn hủy.
        // Không làm mất lịch khám của khách hàng khi hủy một vị trí làm việc.
        var appointments = await db.Appointments
            .Where(x => x.VeterinarianId == shift.VeterinarianId &&
                        x.Status != AppointmentStatus.Canceled &&
                        x.StartsAt < shift.EndAt && x.EndsAt > shift.StartAt)
            .OrderBy(x => x.StartsAt)
            .ToListAsync();

        if (appointments.Count > 0)
        {
            if (!receiveDoctorId.HasValue || receiveDoctorId.Value <= 0 ||
                receiveDoctorId.Value == shift.VeterinarianId)
            {
                TempData["Error"] =
                    $"Bác sĩ này còn {appointments.Count} lịch khám. " +
                    "Hãy chọn bác sĩ khác ĐÃ ĐƯỢC XẾP TRONG CA để nhận lịch trước khi hủy.";
                return Back(shift.StartAt);
            }

            if (appointments.Any(x =>
                    x.Status != AppointmentStatus.Pending &&
                    x.Status != AppointmentStatus.Confirmed &&
                    x.Status != AppointmentStatus.Waiting))
            {
                TempData["Error"] =
                    "Có lịch đang khám, chờ thanh toán hoặc đã hoàn thành. " +
                    "Không thể chuyển các lịch này chỉ bằng thao tác hủy ca.";
                return Back(shift.StartAt);
            }

            if (appointments.Any(x =>
                    x.StartsAt < shift.StartAt || x.EndsAt > shift.EndAt))
            {
                TempData["Error"] =
                    "Có lịch khám nằm ngoài phạm vi ca cần hủy. " +
                    "Hãy điều chỉnh lịch khám trước khi hủy ca.";
                return Back(shift.StartAt);
            }

            var receiverIsActive = await db.Veterinarians.AnyAsync(x =>
                x.Id == receiveDoctorId.Value && x.IsActive);
            if (!receiverIsActive)
            {
                TempData["Error"] = "Bác sĩ nhận lịch không còn hoạt động.";
                return Back(shift.StartAt);
            }

            // Bác sĩ nhận lịch đã có phân công WorkShift bao trùm ca cũ.
            // Không tạo WorkShift mới, không làm tăng số bác sĩ trong ca.
            var receiverHasShift = await db.WorkShifts.AnyAsync(x =>
                x.Id != shift.Id &&
                x.VeterinarianId == receiveDoctorId.Value &&
                x.StartAt <= shift.StartAt && x.EndAt >= shift.EndAt);
            if (!receiverHasShift)
            {
                TempData["Error"] =
                    "Bác sĩ nhận lịch chưa được phân công bao trùm ca cần hủy. " +
                    "Chỉ chọn bác sĩ đã có mặt trong cùng ca.";
                return Back(shift.StartAt);
            }

            // Phòng việc bác sĩ mới đang có khách khám tại cùng thời điểm.
            foreach (var appointment in appointments)
            {
                var busy = await db.Appointments.AnyAsync(x =>
                    x.VeterinarianId == receiveDoctorId.Value &&
                    x.Status != AppointmentStatus.Canceled &&
                    x.StartsAt < appointment.EndsAt &&
                    x.EndsAt > appointment.StartsAt);
                if (busy)
                {
                    TempData["Error"] =
                        $"Bác sĩ nhận lịch đang bận vào giờ của lịch khám #{appointment.Id}. " +
                        "Hãy chọn bác sĩ khác hoặc đổi giờ khám trước.";
                    return Back(shift.StartAt);
                }
            }

            foreach (var appointment in appointments)
            {
                appointment.VeterinarianId = receiveDoctorId.Value;
            }
        }

        // Chỉ đánh dấu đăng ký Canceled nếu không còn phân công cùng ngày/ca
        // cho bác sĩ này (tránh lệch trạng thái khi dữ liệu cũ bị trùng).
        var sameAssignmentRemaining = await db.WorkShifts.AnyAsync(x =>
            x.Id != shift.Id &&
            x.VeterinarianId == shift.VeterinarianId &&
            x.StartAt == shift.StartAt && x.EndAt == shift.EndAt);

        if (!sameAssignmentRemaining)
        {
            var request = await db.DoctorShiftRequests.FirstOrDefaultAsync(x =>
                x.VeterinarianId == shift.VeterinarianId &&
                x.StartAt == shift.StartAt && x.EndAt == shift.EndAt &&
                x.Status == ShiftRequestStatus.Assigned);
            if (request is not null)
            {
                request.Status = ShiftRequestStatus.Canceled;
                request.ReviewNote = LimitNote("Hủy phân công: " + form.Reason.Trim());
                request.ReviewedAtUtc = DateTime.UtcNow;
                request.ReviewedByUserId = users.GetUserId(User);
            }
        }

        db.WorkShifts.Remove(shift);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = appointments.Count > 0
            ? $"Đã hủy một phân công bác sĩ và chuyển {appointments.Count} lịch khám " +
              "sang bác sĩ nhận lịch."
            : "Đã hủy một phân công bác sĩ. Không ảnh hưởng các bác sĩ khác trong ca.";
        return Back(shift.StartAt);
    }

    private static string LimitNote(string note) =>
        note.Length > 400 ? note[..400] : note;

    private IActionResult Back(DateTime? date = null)
    {
        var d = date.HasValue && date.Value.Year >= 2020 && date.Value.Year <= 2100
            ? date.Value : DateTime.Today;
        return RedirectToAction(nameof(Index), new { year = d.Year, month = d.Month });
    }
}
