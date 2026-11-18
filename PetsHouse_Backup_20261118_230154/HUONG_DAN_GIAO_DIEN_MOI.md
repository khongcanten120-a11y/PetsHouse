# Pets House – Giao diện khách hàng & quản trị theo mẫu

Đây là **mã nguồn ASP.NET Core MVC .NET 9**, không phải ảnh giao diện. Bản nâng cấp kế thừa database `QuanLyPhongKhamThuCungDB` và các controller nghiệp vụ của dự án PetsHouse hiện tại.

## 1. Những phần đã thiết kế lại

- `Views/Shared/_Layout.cshtml`: Header khách hàng, sidebar xanh dành cho quản trị viên/nhân viên/bác sĩ/kho; icon SVG, menu mobile, đăng xuất, chuyển sáng/tối.
- `Views/Home/Index.cshtml`: Trang chủ xanh/trắng, hình chó/mèo, giới thiệu ba bước khám, đặt lịch, khối lý do chọn Pets House, quy trình khám, loại khám lấy từ SQL Server.
- `Areas/Admin/Views/Dashboard/Index.cshtml`: Dashboard có thẻ trạng thái, bảng lịch gần nhất, biểu đồ 7 ngày, lịch trong ngày và liên kết quản lý.
- `Areas/Admin/Controllers/DashboardController.cs`: Truy vấn số liệu thật, ngày thống kê, danh sách lịch và biểu đồ từ Entity Framework Core.
- `Areas/Admin/Controllers/AppointmentsController.cs`: Bộ lọc ngày, trạng thái, tìm tên khách hàng/thú cưng và phân trang.
- Các trang quản trị bổ sung: `Admin/Pets`, `Admin/MedicalRecords`, `Admin/Medicines`, `Admin/Invoices` là các **trang tra cứu** dữ liệu thực, không có nút sửa/xóa giả.
- Các View `Appointments/Create`, `Appointments/Index`, `Customer/Index`, `Customer/Profile`, `Account/Login` được viết lại thành nhiều dòng có thụt lề rõ ràng.
- `Styles/base.css` và `wwwroot/css/site.css`: CSS responsive có thể đọc được, không dồn thành một dòng.
- `wwwroot/js/site.js`: Menu mobile, sidebar thu gọn, dark mode, chọn ngày lọc, ghép ngày và giờ thành giá trị `StartsAt` khi đặt lịch trên trang chủ.

## 2. Lưu ý quan trọng về chức năng đặt lịch

- Khách **chưa đăng nhập** thấy giao diện đặt lịch ở trạng thái chưa được phép sử dụng và nút đăng nhập. Không thu thập thông tin rồi bỏ đi.
- Khách **đã đăng nhập và có thú cưng** thấy form thật, gửi POST đến `AppointmentsController.Create` bằng Anti-forgery Token của MVC.
- Chỉ loại khám đang hoạt động trong SQL Server mới có trong danh sách lựa chọn.
- Phải tạo hồ sơ thú cưng trước khi đặt lịch. Backend tiếp tục kiểm tra xung đột với lịch bác sĩ.
- Không đặt cọc, không thanh toán lúc đặt lịch, không có Spa.
- Thanh toán vẫn do nhân viên thực hiện sau khi bác sĩ lưu phiếu khám.

## 3. Cài đặt trên VS Code

1. Sao lưu dự án `PetsHouse` cũ của bạn vào một thư mục khác.
2. Giải nén `PetsHouse_GiaoDien_Moi_DayDu_Source.zip`.
3. Trong VS Code, **Open Folder** và chọn thư mục chứa `PetsHouse.csproj`.
4. Mở Terminal trong thư mục đó.
5. Chạy các lệnh sau:

```powershell
dotnet restore
dotnet build
dotnet run --launch-profile https
```

6. Mở địa chỉ mà Terminal báo `Now listening on: ...`.

### Trường hợp không tìm được project

```powershell
Get-ChildItem -Recurse -Filter *.csproj | Select-Object FullName
```

Bạn phải thực hiện lệnh dotnet trong thư mục chứa `PetsHouse.csproj`.

## 4. Kết nối cơ sở dữ liệu

Project vẫn sử dụng `UserSecretsId` cũ. Đảm bảo `ConnectionStrings:DefaultConnection` trỏ đúng instance SQL Server đang có dữ liệu:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=QuanLyPhongKhamThuCungDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Nếu database đã được tạo bằng Migration, **không chạy lại script tạo bảng và không tạo Migration mới**. Bản nâng cấp giao diện không thêm cột/bảng vào SQL Server.

Nếu tài khoản Admin chưa có, cấu hình `SeedAdmin:Email` và `SeedAdmin:Password` trong User Secrets bằng mật khẩu riêng mạnh (chữ hoa, thường, số, ký tự đặc biệt), sau đó chạy website. Không ghi mật khẩu vào GitHub.

## 5. CSS và JavaScript

File `wwwroot/css/site.css` đã nằm sẵn trong ZIP nên website có kiểu dáng ngay cả khi chưa chạy Node.js.

Nếu bạn muốn build Tailwind CSS production:

```powershell
npm install
npm run build:css
```

Tailwind lấy `Styles/input.css` làm đầu vào và tích hợp nội dung `Styles/base.css`. Tất cả CSS/JS bổ sung được lưu thành nhiều dòng, rõ ràng.

## 6. Đường dẫn cần kiểm tra

- Trang chủ khách hàng: `/`
- Đăng nhập: `/Account/Login`
- Khách hàng: `/Customer`
- Lịch hẹn: `/Appointments`
- Đặt lịch: `/Appointments/Create`
- Admin Dashboard: `/Admin/Dashboard`
- Quản lý lịch hẹn: `/Admin/Appointments`
- Tra cứu thú cưng: `/Admin/Pets`
- Tra cứu bệnh án: `/Admin/MedicalRecords`
- Danh mục thuốc: `/Admin/Medicines`
- Hóa đơn: `/Admin/Invoices`
- Thống kê doanh thu: `/Admin/Reports`
- Giao diện nhân viên: `/Staff/Dashboard`
- Giao diện bác sĩ: `/Doctor/Dashboard`
- Giao diện kho: `/Warehouse/Dashboard`

Các khu vực nội bộ bắt buộc phải đăng nhập đúng vai trò. Các màn hình quản trị bổ sung là màn hình tra cứu, không phải màn hình CRUD đầy đủ.

## 7. Kiểm tra

- Trang chủ hiển thị ảnh chó/mèo; khách chưa đăng nhập thấy nút đăng nhập.
- Khách sau khi đăng nhập, có hồ sơ thú cưng, có thể chọn thú cưng/loại khám/ngày giờ và gửi yêu cầu đặt lịch.
- Admin thấy số liệu thực tế từ SQL Server. Nếu database trống, số lượng bằng 0 và bảng hiển thị trạng thái trống, không phải dữ liệu giả.
- Thử lọc ngày tại Dashboard, tìm khách hàng/thú cưng ở trang lịch hẹn, chuyển sáng/tối và thu gọn sidebar.

## 8. Tình trạng kiểm thử

Mã nguồn đã được kiểm tra cấu trúc cơ bản, cú pháp JavaScript, nội dung CSS, SVG và đường dẫn file. **Chưa kiểm thử biên dịch bằng .NET 9 SDK hoặc kết nối thực với SQL Server trong môi trường tạo file này**. Bạn hãy chạy `dotnet build` trên máy đã cài .NET 9; nếu có lỗi, chụp phần thông báo đầu tiên của lỗi để xác định file/dòng cần xử lý.
