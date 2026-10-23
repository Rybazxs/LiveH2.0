# LiveH OS 2.0 | PATCH-006 - swipe navigation + overdue colors
$ErrorActionPreference = 'Stop'
$root = 'E:\Private\Projekty\LiveH OS 2.0'
$targets = @(
    @{ Relative = 'src\LiveH.App\MainPage.xaml'; Before = '66d7a4bbe8e510d8db478cea7477cfb0ce869123dcf54b838a10d8df6f96ae8d'; After = '61c94208fb65c8cc9074435be7915045969b16af2ddb6a84971a6465390ca94d' }
    @{ Relative = 'src\LiveH.App\MainPage.xaml.cs'; Before = '1c7a57f861515c77282c2357fe4c134e6735a2afebfde0f00f2b613e857ffe90'; After = '512f6d67cce12da292cd1b0ff99bf68318f552631b7deeecd328730b557c4a0a' }
    @{ Relative = 'src\LiveH.App\Views\TodayView.xaml'; Before = 'b24a606ab7c878e34e4a59e5eff0e50997fff6b14abaceead6ab5975e0dd2570'; After = 'fbf91527c8a24872c34fb7ccec8ccc6cc3b52d3a39a022425c78c9f43a7314a5' }
    @{ Relative = 'src\LiveH.App\Views\TodayView.xaml.cs'; Before = '89a976651a11e06b101c0e37f15c1bfc41c60a7b1e05006190320fa3a47b7766'; After = 'caf1f0efdecb75447f465ec24d086326b6d2f57ed4ae3cad5c5a4d8e8d7bef38' }
)
try {
    if (-not (Test-Path -LiteralPath (Join-Path $root 'LiveH.sln'))) {
        throw 'Nie znaleziono LiveH.sln w katalogu projektu.'
    }
    $already = 0
    foreach ($t in $targets) {
        $t.Source = Join-Path $PSScriptRoot ('files\' + $t.Relative)
        $t.Target = Join-Path $root $t.Relative
        if (-not (Test-Path -LiteralPath $t.Source)) { throw ('Brak pliku paczki: ' + $t.Relative) }
        if (-not (Test-Path -LiteralPath $t.Target)) { throw ('Brak pliku projektu: ' + $t.Relative) }
        $srcHash = (Get-FileHash -LiteralPath $t.Source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($srcHash -ne $t.After) { throw ('Uszkodzony plik paczki: ' + $t.Relative) }
        $dstHash = (Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($dstHash -eq $t.After) { $already++; continue }
        if ($dstHash -ne $t.Before) {
            throw ('Nieznana wersja: ' + $t.Relative + '. Nie nadpisuje plikow. Sprawdz PATCH-005 i lokalne zmiany.')
        }
    }
    if ($already -eq $targets.Count) {
        Write-Host '[LIVEH] PATCH-006 jest juz zainstalowany.' -ForegroundColor Green
        exit 0
    }
    if ($already -gt 0) {
        throw 'Wykryto czesciowo zainstalowany patch. Nie nadpisuje automatycznie.'
    }

    $backupDir = Join-Path $root ('zip\_backups\PATCH-006-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    foreach ($t in $targets) {
        $backup = Join-Path $backupDir $t.Relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $t.Target -Destination $backup -ErrorAction Stop
    }
    $changed = @()
    try {
        foreach ($t in $targets) {
            Copy-Item -LiteralPath $t.Source -Destination $t.Target -Force -ErrorAction Stop
            $changed += $t
            $actual = (Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -ne $t.After) { throw ('Nie udalo sie zapisac: ' + $t.Relative) }
        }
    } catch {
        foreach ($t in $changed) {
            Copy-Item -LiteralPath (Join-Path $backupDir $t.Relative) -Destination $t.Target -Force
        }
        throw
    }
    Write-Host '[LIVEH] SUKCES: PATCH-006 - SWIPE + GREEN OVERDUE.' -ForegroundColor Green
    Write-Host ('[LIVEH] Kopia: ' + $backupDir)
    Write-Host '[LIVEH] Teraz w VS: Build > Rebuild Solution > F5'
} catch {
    Write-Host ('[LIVEH BLAD] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
