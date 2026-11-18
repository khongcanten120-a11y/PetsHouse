# PetsHouse – ASP.NET Core MVC 9 + SQL Server

Đây là **source code thực tế** cho đồ án hệ thống phòng khám thú cưng, được đóng gói cùng file SQL riêng `PetsHouse_SQL_HoanChinh.sql` (bên ngoài ZIP).

**Phạm vi:** khách hàng đăng ký và quản lý thú cưng; đặt lịch không đặt cọc; nhân viên xác nhận và phân bác sĩ; bác sĩ khám và kê thuốc; quản lý kho nhập/cấp phát; nhân viên thanh toán sau khám; quản trị viên quản lý tài khoản, loại khám, xem báo cáo. **Không có Spa**. Bảng Vaccinations được giữ để tương thích code cũ nhưng không phải chức năng cần trình diễn.

## Cài đặt trên VS Code (Windows)

1. Giải nén ZIP. Mở **đúng thư mục `PetsHouse`**, là nơi có `PetsHouse.csproj` và `Program.cs`.
2. Cài .NET 9 SDK, SQL Server + SSMS. Trong SSMS, chọn đúng server instance (ví dụ `.\SQLEXPRESS`), rồi mở file **`PetsHouse_SQL_HoanChinh.sql`** và nhấn Execute (F5). **Nếu database đã tồn tại với đầy đủ bảng (như máy của bạn), không cần chạy script lại.**
3. Mở Terminal tại thư mục có file `.csproj`:

```powershell
dotnet restore
# Ghi chu: project sử dụng user secrets để không lưu mật khẩu trong source.
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=.\SQLEXPRESS;Database=QuanLyPhongKhamThuCungDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
dotnet build
```

4. Để tạo tài khoản Admin trong máy **mới**, nhập email và mật khẩu mạnh vào User Secrets:

```powershell
dotnet user-secrets set 'SeedAdmin:Email' 'admin@local.test'
$secure = Read-Host 'Nhap mat khau Admin (khong hien ky tu)' -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $secure).Password
dotnet user-secrets set 'SeedAdmin:Password' $plain
Remove-Variable plain,secure
```

Mật khẩu phải có chữ hoa, chữ thường, chữ số, ký tự đặc biệt, dài tối thiểu 8 ký tự. Không chia sẻ mật khẩu trong ảnh chụp hoặc đưa lên GitHub.

5. Chạy:

```powershell
dotnet run --launch-profile https
```

Mở URL **do Terminal hiển thị** (thường là https://localhost:7197).

### Nếu SQL đã có database từ Migration
Project nguồn này **không chứa thư mục Migrations gốc của máy bạn**. Không chạy `dotnet ef migrations add InitialCreate` khi database đã có bảng. File .sql đi kèm đủ cấu trúc cho cài đặt độc lập. Nếu muốn phát triển Migration mới, hãy thiết lập baseline và đồng bộ với database trước.

### CSS
`wwwroot/css/site.css` được đóng gói sẵn. Có thể rebuild bằng `npm install` và `npm run build:css` (cần Node.js) khi chỉnh Tailwind.

### Lưu ý về chất lượng
Bộ code được rà soát/sửa lỗi Razor `@page` và `ExamineVm.Items` đã được báo trước đó. Môi trường tạo file không có .NET SDK và SQL Server, nên **chưa thể xác nhận `dotnet build`/kiểm thử thực tế**. Đây là phiên bản đồ án để chạy và kiểm thử tại máy bạn, không cam kết production-ready. Đặc biệt quy trình chuyển khoản, nhập xuất thuốc theo lô và kiểm thử concurrency cần kiểm tra thêm nếu dùng ngoài phạm vi đồ án.
