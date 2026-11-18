param(
    [string]$ProjectRoot = 'C:\Users\nptk2\Downloads\Pets_House\PetsHouse'
)

$ErrorActionPreference = 'Stop'
$layoutPath = Join-Path $ProjectRoot 'Views\Shared\_Layout.cshtml'
$menuPath = Join-Path $PSScriptRoot 'MenuNhanVien.cshtml.txt'

if (-not (Test-Path (Join-Path $ProjectRoot 'PetsHouse.csproj'))) {
    throw 'Không tìm thấy PetsHouse.csproj. Hãy kiểm tra đường dẫn ProjectRoot.'
}
if (-not (Test-Path $layoutPath)) {
    throw 'Không tìm thấy Views\Shared\_Layout.cshtml trong dự án.'
}

$layout = Get-Content -Path $layoutPath -Raw -Encoding UTF8
$menu = (Get-Content -Path $menuPath -Raw -Encoding UTF8).Trim()

# Lấy đúng khối Razor, không lấy phần hướng dẫn văn bản.
if (-not $menu.StartsWith('@if (isStaff)')) {
    throw 'Định dạng menu không hợp lệ.'
}

$begin = $layout.IndexOf('@if (isStaff)')
$end = if ($begin -ge 0) { $layout.IndexOf('@if (isDoctor)', $begin) } else { -1 }
if ($begin -lt 0 -or $end -lt 0 -or $end -le $begin) {
    throw 'Không tìm thấy khối menu Staff/Doctor để thay an toàn. Hãy sửa thủ công theo file menu.'
}

$indentStart = $layout.LastIndexOf("`n", [Math]::Max(0, $begin - 1)) + 1
$indent = $layout.Substring($indentStart, $begin - $indentStart)
if ($indent.Trim().Length -ne 0) {
    throw 'Cấu trúc menu khác dự kiến; không tự thay.'
}

$menuLines = $menu -split "`r?`n"
$menuIndented = ($menuLines | ForEach-Object { $indent + $_ }) -join "`r`n"

$backup = "$layoutPath.backup_$(Get-Date -Format yyyyMMdd_HHmmss)"
Copy-Item -LiteralPath $layoutPath -Destination $backup -ErrorAction Stop

$updated = $layout.Substring(0, $begin) + $menuIndented + "`r`n" + $indent + $layout.Substring($end)
[System.IO.File]::WriteAllText(
    $layoutPath,
    $updated,
    (New-Object System.Text.UTF8Encoding($false))
)

$required = @(
    'Areas\Staff\Controllers\ReceptionController.cs',
    'Areas\Staff\Controllers\AppointmentsController.cs',
    'Areas\Staff\Controllers\WorkShiftsController.cs',
    'Areas\Staff\Controllers\InvoicesController.cs',
    'Areas\Staff\Controllers\PaymentsController.cs'
)
$missing = @($required | Where-Object { -not (Test-Path (Join-Path $ProjectRoot $_)) })
Write-Host 'Đã cập nhật menu Staff có 6 chức năng.' -ForegroundColor Green
Write-Host "Đã sao lưu Layout: $backup"
if ($missing.Count -gt 0) {
    Write-Warning 'Một số Controller chưa có trong dự án, các liên kết tương ứng có thể báo lỗi 404:'
    $missing | ForEach-Object { Write-Warning $_ }
}
Write-Host 'Dừng ứng dụng, chạy dotnet build, sau đó dotnet run --launch-profile https.'
