-- Chạy MỘT LẦN tại SSMS, trên database PetsHouse đang dùng.
-- Không sửa dữ liệu WorkShifts, Appointments, bác sĩ hay các hóa đơn cũ.
USE [QuanLyPhongKhamThuCungDB];
GO
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.DoctorShiftRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DoctorShiftRequests
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VeterinarianId INT NOT NULL,
        StartAt DATETIME2(7) NOT NULL,
        EndAt DATETIME2(7) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_DoctorShiftRequests_Status DEFAULT N'Pending',
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_DoctorShiftRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
        ReviewedAtUtc DATETIME2(7) NULL,
        ReviewedByUserId NVARCHAR(450) NULL,
        ReviewNote NVARCHAR(400) NULL,
        CONSTRAINT FK_DoctorShiftRequests_Veterinarians
            FOREIGN KEY (VeterinarianId) REFERENCES dbo.Veterinarians(Id),
        CONSTRAINT CK_DoctorShiftRequests_DateRange CHECK (EndAt > StartAt),
        CONSTRAINT CK_DoctorShiftRequests_Status CHECK
            (Status IN (N'Pending', N'Accepted', N'Assigned', N'Rejected',
                        N'Withdrawn', N'Replaced', N'Canceled'))
    );
END;
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DoctorShiftRequests')
      AND name = N'UX_DoctorShiftRequests_DoctorAndTime')
BEGIN
    CREATE UNIQUE INDEX UX_DoctorShiftRequests_DoctorAndTime
        ON dbo.DoctorShiftRequests(VeterinarianId, StartAt, EndAt);
END;
GO
SELECT TOP (5) Id, VeterinarianId, StartAt, EndAt, Status
FROM dbo.DoctorShiftRequests ORDER BY Id DESC;
GO
PRINT N'Đã kiểm tra/tạo bảng yêu cầu đăng ký ca. WorkShifts cũ được giữ nguyên.';
