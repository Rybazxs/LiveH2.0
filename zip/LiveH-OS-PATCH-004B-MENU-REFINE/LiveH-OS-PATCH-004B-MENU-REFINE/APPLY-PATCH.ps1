# LiveH OS / PATCH-004B - faithful compact menu
# Supports both original Foundation menu and previously-applied PATCH-004.
$ErrorActionPreference = 'Stop'
$projectRoot = 'E:\Private\Projekty\LiveH OS 2.0'
$packageRoot = $PSScriptRoot
$items = @(
    @{ Relative = "src/LiveH.App/MainPage.xaml"; New = "bb133b515e16db601fe12e6ee72759b10ac80415667bd4b26661bff277b35e0c"; Allowed = @("7981626c2f81924332b8abca9deb940a7fd6f6e23f9f77c8ee5bf229568586bc", "942838fe42c5b146d08695b69a8907b60b2da84a228fea8b2eb0ee3eb2b650cf") },
    @{ Relative = "src/LiveH.App/MainPage.xaml.cs"; New = "1c7a57f861515c77282c2357fe4c134e6735a2afebfde0f00f2b613e857ffe90"; Allowed = @("1c7a57f861515c77282c2357fe4c134e6735a2afebfde0f00f2b613e857ffe90", "63dfae9bec742fec63308a217bc6fa45f0e28aa15d89ad4efbe7fdf8aedfd8bb") }
)
function Hash-File([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
try {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'LiveH.sln'))) {
        throw 'Nie znaleziono LiveH.sln. Projekt musi byc w ustalonym katalogu.'
    }
    $changes = @()
    foreach ($item in $items) {
        $source = Join-Path $packageRoot ('files\' + $item.Relative.Replace('/','\'))
        $target = Join-Path $projectRoot $item.Relative
        if (-not (Test-Path -LiteralPath $source)) { throw "Brak pliku patcha: $source" }
        if (-not (Test-Path -LiteralPath $target)) { throw "Brak pliku projektu: $target" }
        if ((Hash-File $source) -ne $item.New) { throw "Uszkodzony plik poprawki: $source" }
        $current = Hash-File $target
        if ($current -eq $item.New) { continue }
        if ($current -notin $item.Allowed) {
            throw "Plik projektu ma nieznane zmiany. Nie nadpisuje: $target"
        }
        $changes += $item
    }
    if ($changes.Count -eq 0) {
        Write-Host '[LIVEH] PATCH-004B juz obecny. Nie ma zmian.' -ForegroundColor Green
        exit 0
    }
    $backupDir = Join-Path $projectRoot ('zip\_backups\PATCH-004B-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    foreach ($item in $changes) {
        $target = Join-Path $projectRoot $item.Relative
        $backup = Join-Path $backupDir $item.Relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup -ErrorAction Stop
    }
    $written = @()
    try {
        foreach ($item in $changes) {
            $source = Join-Path $packageRoot ('files\' + $item.Relative.Replace('/','\'))
            $target = Join-Path $projectRoot $item.Relative
            Copy-Item -LiteralPath $source -Destination $target -Force -ErrorAction Stop
            $written += $item
        }
    }
    catch {
        foreach ($item in $written) {
            $target = Join-Path $projectRoot $item.Relative
            $backup = Join-Path $backupDir $item.Relative
            Copy-Item -LiteralPath $backup -Destination $target -Force
        }
        throw
    }
    Write-Host '[LIVEH] SUKCES: PATCH-004B - ORYGINALNE MENU, MNIEJSZY ROZMIAR.' -ForegroundColor Green
    Write-Host "[LIVEH] Kopia: $backupDir" -ForegroundColor Gray
    Write-Host '[LIVEH] Visual Studio: Build > Rebuild Solution > F5' -ForegroundColor Cyan
}
catch {
    Write-Host ('[LIVEH BLAD] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
