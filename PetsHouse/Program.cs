using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;
using PetsHouse.Services;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    var keysPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "private",
        "keys"
    );

    Directory.CreateDirectory(keysPath);

    builder.Services.AddDataProtection()
        .SetApplicationName("PetsHouse")
        .PersistKeysToFileSystem(
            new DirectoryInfo(keysPath)
        );
}

string connection;
if (builder.Environment.IsDevelopment())
{
    // SQL Server trên máy tính
    connection =
        @"Server=.\SQLEXPRESS;" +
        "Database=QuanLyPhongKhamThuCungDB;" +
        "Trusted_Connection=True;" +
        "TrustServerCertificate=True;";
}else
{
    // SQL Server trên MonsterASP.NET
    // Database: db81063
    connection = builder.Configuration
        .GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Thiếu kết nối SQL Server hosting db81063."
        );
}
builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlServer(connection)
);

// ========================================
// 2. CẤU HÌNH IDENTITY
// ========================================

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(
    options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.User.RequireUniqueEmail = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ========================================
// 3. CẤU HÌNH COOKIE ĐĂNG NHẬP
// ========================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

// ========================================
// 4. ĐĂNG KÝ SERVICES
// ========================================

builder.Services.AddScoped<BookingService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ========================================
// 5. MIDDLEWARE
// ========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// ========================================
// 7. KIỂM TRA DATABASE VÀ ROLE
// ========================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    if (await db.Database.CanConnectAsync())
    {
        await RoleSeed.EnsureAsync(
            app.Services,
            app.Configuration
        );
    }
    else
    {
        app.Logger.LogWarning(
            "Không thể kết nối SQL Server. " +
            "Vui lòng kiểm tra cấu hình database."
        );
    }
}

app.Run();
