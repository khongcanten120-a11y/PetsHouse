using Microsoft.AspNetCore.Identity;
using PetsHouse.Models;

namespace PetsHouse.Data;

public static class RoleSeed
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PetsHouse.RoleSeed");
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Customer, Roles.Staff, Roles.Doctor, Roles.Warehouse, Roles.Admin })
        {
            if (await roles.RoleExistsAsync(role)) continue;
            var created = await roles.CreateAsync(new IdentityRole(role));
            if (!created.Succeeded)
                throw new InvalidOperationException("Không tạo được vai trò: " +
                    string.Join("; ", created.Errors.Select(x => x.Description)));
        }

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Chưa có SeedAdmin:Email và SeedAdmin:Password. Không tự tạo quản trị viên.");
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, Roles.Admin))
                logger.LogWarning("Email SeedAdmin đã thuộc tài khoản không có quyền Admin. Không nâng quyền tự động.");
            return;
        }

        var newAdmin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Quản trị viên"
        };
        var result = await users.CreateAsync(newAdmin, password);
        if (!result.Succeeded)
        {
            // Không ghi mật khẩu ra log và không khiến website ngừng phục vụ vì cấu hình seed sai.
            logger.LogError("Không tạo được Admin: {Errors}. Hãy sửa SeedAdmin:Password trong User Secrets và khởi động lại.",
                string.Join("; ", result.Errors.Select(x => x.Description)));
            return;
        }
        var addRole = await users.AddToRoleAsync(newAdmin, Roles.Admin);
        if (!addRole.Succeeded)
        {
            await users.DeleteAsync(newAdmin);
            logger.LogError("Tạo Admin nhưng chưa gán được role: {Errors}",
                string.Join("; ", addRole.Errors.Select(x => x.Description)));
            return;
        }
        logger.LogInformation("Đã khởi tạo tài khoản quản trị.");
    }
}
