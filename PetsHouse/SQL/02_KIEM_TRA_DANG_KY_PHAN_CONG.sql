USE [QuanLyPhongKhamThuCungDB];
GO

-- 1. Tất cả yêu cầu bác sĩ đăng ký gần nhất.
SELECT TOP (100)
    r.Id AS MaDangKy,
    v.FullName AS BacSi,
    r.StartAt AS BatDau,
    r.EndAt AS KetThuc,
    r.Status AS TrangThai,
    r.ReviewNote AS GhiChuXuLy
FROM dbo.DoctorShiftRequests AS r
JOIN dbo.Veterinarians AS v ON v.Id = r.VeterinarianId
ORDER BY r.StartAt DESC, r.Id DESC;
GO

-- 2. Ca đã phân công chính thức trong tháng 11/2026.
SELECT
    w.Id AS MaCa,
    v.FullName AS BacSi,
    w.StartAt AS BatDau,
    w.EndAt AS KetThuc
FROM dbo.WorkShifts AS w
JOIN dbo.Veterinarians AS v ON v.Id = w.VeterinarianId
WHERE w.StartAt >= '20261101' AND w.StartAt < '20261201'
ORDER BY w.StartAt, w.Id;
GO

-- 3. Kiểm tra tất cả yêu cầu chưa được phân công.
SELECT r.Status, COUNT(*) AS SoLuong
FROM dbo.DoctorShiftRequests AS r
GROUP BY r.Status
ORDER BY r.Status;
GO


