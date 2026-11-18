param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $ProjectRoot).Path
$projectFile = Join-Path $root 'PetsHouse.csproj'
if (-not (Test-Path $projectFile)) {
    throw "Không tìm thấy PetsHouse.csproj tại $root. Hãy chọn đúng thư mục project."
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$files = @(
    'Models/StaffOperationsVms.cs',
    'Areas/Staff/Controllers/ReceptionController.cs',
    'Areas/Staff/Controllers/AppointmentsController.cs',
    'Areas/Staff/Controllers/WorkShiftsController.cs',
    'Areas/Staff/Controllers/InvoicesController.cs',
    'Areas/Staff/Views/Reception/Index.cshtml',
    'Areas/Staff/Views/Reception/Create.cshtml',
    'Areas/Staff/Views/Reception/AddPet.cshtml',
    'Areas/Staff/Views/Appointments/Index.cshtml',
    'Areas/Staff/Views/Appointments/Create.cshtml',
    'Areas/Staff/Views/Appointments/Edit.cshtml',
    'Areas/Staff/Views/WorkShifts/Index.cshtml',
    'Areas/Staff/Views/Invoices/Index.cshtml'
)

foreach ($relative in $files) {
    $source = Join-Path $PSScriptRoot $relative
    $target = Join-Path $root $relative
    $folder = Split-Path -Parent $target
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    if (Test-Path $target) {
        Copy-Item -Path $target -Destination "$target.backup_$stamp"
    }
    Copy-Item -Path $source -Destination $target -Force
    Write-Host "Đã cập nhật: $relative"
}

# Thay riêng đoạn menu Staff, giữ nguyên các sửa đổi hiện tại của menu Doctor/Admin.
$layoutPath = Join-Path $root 'Views/Shared/_Layout.cshtml'
if (-not (Test-Path $layoutPath)) {
    throw 'Không tìm thấy _Layout.cshtml. Hãy xem hướng dẫn dán menu thủ công.'
}

$menuDoc = Get-Content -Path (Join-Path $PSScriptRoot 'HuongDan/Menu_NhanVien.cshtml.txt') -Raw -Encoding UTF8
$menuStart = $menuDoc.IndexOf('@if (isStaff)')
if ($menuStart -lt 0) {
    throw 'Thiếu phần menu Staff trong file hướng dẫn.'
}
$menuReplacement = $menuDoc.Substring($menuStart).TrimEnd() + "`r`n"

$layout = Get-Content -Path $layoutPath -Raw -Encoding UTF8
$beginMarker = '@if (isStaff)'
$endMarker = '@if (isDoctor)'
$begin = $layout.IndexOf($beginMarker)
$end = if ($begin -ge 0) { $layout.IndexOf($endMarker, $begin) } else { -1 }
if ($begin -lt 0 -or $end -lt 0) {
    Write-Warning 'Không tìm thấy menu Staff theo định dạng gốc. Hãy chèn theo HuongDan/Menu_NhanVien.cshtml.txt.'
} else {
    $indent = '                '
    $replaced = $layout.Substring(0, $begin) + $menuReplacement + $indent + $layout.Substring($end)
    Copy-Item -Path $layoutPath -Destination "$layoutPath.backup_$stamp"
    [System.IO.File]::WriteAllText(
        $layoutPath,
        $replaced,
        (New-Object System.Text.UTF8Encoding($false))
    )
    Write-Host 'Đã thêm 6 menu nhân viên; đã sao lưu _Layout.cshtml.'
}

Write-Host ''
Write-Host 'Hoàn tất sao chép. Chạy dotnet build rồi dotnet run --launch-profile https trong thư mục project.'
Write-Host 'Không chạy migration hoặc câu lệnh xóa dữ liệu SQL.'
