param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path $ProjectPath).Path
$root = $PSScriptRoot

if (-not (Test-Path (Join-Path $project 'PetsHouse.csproj'))) {
    throw 'Khong tim thay PetsHouse.csproj tai ProjectPath.'
}

if (Test-Path (Join-Path $project 'PetsHouse\PetsHouse.csproj')) {
    throw 'Co du an PetsHouse long ben trong. Hay di chuyen ban sao ra ngoai truoc.'
}
$sourceFiles = @(
    'Models\DoctorShiftRequest.cs',
    'Models\ShiftCatalog.cs',
    'Models\ShiftCalendarVm.cs',
    'Models\ShiftCalendarFactory.cs',
    'Areas\Doctor\Controllers\WorkShiftsController.cs',
    'Areas\Doctor\Views\WorkShifts\Index.cshtml',
    'Areas\Staff\Controllers\WorkShiftsController.cs',
    'Areas\Staff\Views\WorkShifts\Index.cshtml',
    'wwwroot\css\pets-shift-calendar.css'
)

$backupRoot = Join-Path (Split-Path $project -Parent) ('PetsHouse_Backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

foreach ($relative in $sourceFiles) {
    $src = Join-Path $root $relative
    $dst = Join-Path $project $relative
    $backupPath = Join-Path $backupRoot $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
    if (Test-Path $dst) {
        New-Item -ItemType Directory -Force -Path (Split-Path $backupPath -Parent) | Out-Null
        Copy-Item -Path $dst -Destination $backupPath -Force
    }
    Copy-Item -Path $src -Destination $dst -Force
}

# AppDbContext co the da duoc nguoi dung sua: sua moi 2 cho, khong ghi de toan file.
$dbFile = Join-Path $project 'Data\AppDbContext.cs'
if (-not (Test-Path $dbFile)) {
    throw 'Khong thay Data\AppDbContext.cs; can sua thu cong theo HuongDan.'
}
$original = [System.IO.File]::ReadAllText($dbFile)
$updated = $original
if (-not $updated.Contains('DbSet<DoctorShiftRequest>')) {
    $marker = 'public DbSet<WorkShift> WorkShifts => Set<WorkShift>();'
    if (-not $updated.Contains($marker)) {
        throw 'Khong tim thay dong DbSet<WorkShift>. Hay sua AppDbContext thu cong.'
    }
    $updated = $updated.Replace($marker, $marker + "`r`n    public DbSet<DoctorShiftRequest> DoctorShiftRequests => Set<DoctorShiftRequest>();")
}
if (-not $updated.Contains('UX_DoctorShiftRequests_DoctorAndTime')) {
    $marker = 'base.OnModelCreating(model);'
    if (-not $updated.Contains($marker)) {
        throw 'Khong tim thay OnModelCreating. Hay sua AppDbContext thu cong.'
    }
    $map = @'

        model.Entity<DoctorShiftRequest>()
            .HasIndex(x => new { x.VeterinarianId, x.StartAt, x.EndAt })
            .IsUnique()
            .HasDatabaseName("UX_DoctorShiftRequests_DoctorAndTime");

        model.Entity<DoctorShiftRequest>()
            .HasOne(x => x.Veterinarian)
            .WithMany()
            .HasForeignKey(x => x.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);
'@
    $updated = $updated.Replace($marker, $marker + $map)
}
if ($updated -ne $original) {
    Copy-Item $dbFile (Join-Path $backupRoot 'AppDbContext.cs.bak') -Force
    [System.IO.File]::WriteAllText($dbFile, $updated, (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host "Da copy bo lich va them DbContext. Backup: $backupRoot" -ForegroundColor Green
Write-Host 'TIEP THEO: Mo SQL\01_TAO_BANG_DANG_KY_CA.sql trong SSMS va bam F5 truoc khi mo website.'
Write-Host 'Roi chay: dotnet build; dotnet run --launch-profile https'
Write-Host 'Admin: su dung /Staff/WorkShifts; xem huong dan neu con form Shift cua Admin.'
