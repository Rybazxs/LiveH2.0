# LiveH OS 2.0 / PATCH-005 - visual refinement only
$ErrorActionPreference = 'Stop'
$root = 'E:\Private\Projekty\LiveH OS 2.0'
$targets = @(
    @{ Relative = 'src\LiveH.App\MainPage.xaml'; Before = 'c6ba10a6412d2d8f82196f77d4f5a7080294cdc8da58187ec504d73bdb5c3efb'; After = '66d7a4bbe8e510d8db478cea7477cfb0ce869123dcf54b838a10d8df6f96ae8d' }
    @{ Relative = 'src\LiveH.App\Views\TodayView.xaml'; Before = 'c04dadad7acf73dca4ac6c3840f07aca8a9689b319d1b38a1f44d2ca92145040'; After = 'b24a606ab7c878e34e4a59e5eff0e50997fff6b14abaceead6ab5975e0dd2570' }
)
try {
    if (-not (Test-Path -LiteralPath (Join-Path $root 'LiveH.sln'))) {
        throw 'Brak LiveH.sln. Sprawdz katalog projektu.'
    }
    $already = 0
    foreach ($t in $targets) {
        $t.Source = Join-Path $PSScriptRoot ('files\' + $t.Relative)
        $t.Target = Join-Path $root $t.Relative
        if (-not (Test-Path -LiteralPath $t.Source)) { throw ('Brak pliku w paczce: ' + $t.Relative) }
        if (-not (Test-Path -LiteralPath $t.Target)) { throw ('Brak pliku projektu: ' + $t.Relative) }
        $sourceHash = (Get-FileHash -LiteralPath $t.Source -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($sourceHash -ne $t.After) { throw ('Bledna suma SHA256 paczki: ' + $t.Relative) }
        $targetHash = (Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($targetHash -eq $t.After) { $already++; continue }
        if ($targetHash -ne $t.Before) {
            throw ('Nieznana wersja pliku: ' + $t.Relative + '. Nie nadpisuje. Wymagany PATCH-004C oraz niezmieniony TodayView.')
        }
    }
    if ($already -eq $targets.Count) {
        Write-Host '[LIVEH] PATCH-005 jest juz zainstalowany.' -ForegroundColor Green
        exit 0
    }
    if ($already -gt 0) {
        throw 'Wykryto czesciowo zainstalowany PATCH-005. Nie nadpisuje plikow automatycznie.'
    }
    $backupDir = Join-Path $root ('zip\_backups\PATCH-005-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
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
            $actualHash = (Get-FileHash -LiteralPath $t.Target -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualHash -ne $t.After) { throw ('Weryfikacja zapisu nie powiodla sie: ' + $t.Relative) }
        }
    } catch {
        foreach ($t in $changed) {
            $backup = Join-Path $backupDir $t.Relative
            Copy-Item -LiteralPath $backup -Destination $t.Target -Force
        }
        throw
    }
    Write-Host '[LIVEH] SUKCES: PATCH-005 - UI REFINEMENT.' -ForegroundColor Green
    Write-Host ('[LIVEH] Backup: ' + $backupDir)
    Write-Host '[LIVEH] Visual Studio: Build > Rebuild Solution > F5'
} catch {
    Write-Host ('[LIVEH BLAD] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
