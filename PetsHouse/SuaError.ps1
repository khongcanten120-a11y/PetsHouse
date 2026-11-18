param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectFile = Join-Path $project 'PetsHouse.csproj'
if (-not (Test-Path -LiteralPath $projectFile)) {
    throw 'Khong tim thay PetsHouse.csproj. Vui long kiem tra ProjectPath.'
}

$dbContext = Join-Path $project 'Data\AppDbContext.cs'
if (-not (Test-Path -LiteralPath $dbContext)) {
    throw 'Khong tim thay Data\AppDbContext.cs.'
}

$modelsPath = Join-Path $project 'Models'
if (-not (Test-Path -LiteralPath $modelsPath)) {
    throw 'Khong tim thay Models trong thu muc project.'
}

$backupName = 'PetsHouse_Shift_Backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss')
$backup = Join-Path (Split-Path -Parent $project) $backupName
New-Item -ItemType Directory -Path $backup -Force | Out-Null

function BackupFile($relativePath) {
    $sourcePath = Join-Path $project $relativePath
    if (Test-Path -LiteralPath $sourcePath) {
        $destination = Join-Path $backup $relativePath
        $destinationFolder = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destination -Force
    }
}

# Only the required view-model and calendar factory are replaced.
foreach ($model in @('ShiftCalendarVm.cs', 'ShiftCalendarFactory.cs')) {
    $relative = 'Models\' + $model
    BackupFile $relative
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relative) `
        -Destination (Join-Path $project $relative) -Force
    Write-Host ('Cap nhat: ' + $relative)
}

# DoctorShiftRequest might already exist from a prior patch: never duplicate the type.
$doctorTypeFound = Get-ChildItem -LiteralPath $modelsPath -Filter '*.cs' -File |
    Select-String -Pattern '\bclass\s+DoctorShiftRequest\b' -Quiet
if (-not $doctorTypeFound) {
    $relative = 'Models\DoctorShiftRequest.cs'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $relative) `
        -Destination (Join-Path $project $relative) -Force
    Write-Host ('Bo sung: ' + $relative)
}
else {
    Write-Host 'Da co class DoctorShiftRequest. Khong tao trung.'
}

BackupFile 'Data\AppDbContext.cs'
$content = [System.IO.File]::ReadAllText($dbContext)

if ($content -notmatch 'DbSet\s*<\s*DoctorShiftRequest\s*>\s+DoctorShiftRequests') {
    $needle = 'public DbSet<WorkShift> WorkShifts => Set<WorkShift>();'
    if (-not $content.Contains($needle)) {
        throw 'DbContext khac mau. Da sao luu, chua sua DbContext. Them DbSet thu cong theo README.'
    }
    $replacement = $needle + "`r`n    public DbSet<DoctorShiftRequest> DoctorShiftRequests => Set<DoctorShiftRequest>();"
    $content = $content.Replace($needle, $replacement)
}

if (-not $content.Contains('UX_DoctorShiftRequests_DoctorAndTime')) {
    $needle = 'base.OnModelCreating(model);'
    if (-not $content.Contains($needle)) {
        throw 'Khong thay base.OnModelCreating(model). Da sao luu, vui long them mapping thu cong.'
    }
    $mapping = @'
base.OnModelCreating(model);

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
    $content = $content.Replace($needle, $mapping)
}

[System.IO.File]::WriteAllText(
    $dbContext,
    $content,
    ([System.Text.UTF8Encoding]::new($false))
)

Write-Host ''
Write-Host ('Da cap nhat model va AppDbContext. Sao luu tai: ' + $backup)
Write-Host 'BUOC TIEP: dotnet build'
Write-Host 'Neu build thanh cong nhung chay web bao Invalid object name DoctorShiftRequests:'
Write-Host 'Mo SQL\01_TAO_BANG_DANG_KY_CA.sql trong SSMS, kiem tra dung database roi F5.'
