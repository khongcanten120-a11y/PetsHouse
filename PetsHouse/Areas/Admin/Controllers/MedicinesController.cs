
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetsHouse.Data;
using PetsHouse.Models;

namespace PetsHouse.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class MedicinesController(AppDbContext db) : Controller
{
    // Danh sách thuốc - tìm kiếm và sắp xếp theo ID
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Medicines
            .AsNoTracking()
            .AsQueryable();

        // Kiểm tra từ khóa tìm kiếm
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();

            // Giới hạn độ dài từ khóa
            if (q.Length > 80)
            {
                q = q[..80];
            }

            // Tìm kiếm thuốc theo tên
            query = query.Where(
                medicine => medicine.Name.Contains(q)
            );
        }

        // Giữ lại từ khóa trên giao diện
        ViewBag.Query = q;

        // Sắp xếp theo mã thuốc tăng dần
        var medicines = await query
            .OrderBy(medicine => medicine.Id)
            .Take(300)
            .ToListAsync();

        return View(medicines);
    }
}
