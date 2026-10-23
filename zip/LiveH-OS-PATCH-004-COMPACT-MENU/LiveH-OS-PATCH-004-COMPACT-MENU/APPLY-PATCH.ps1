# LiveH OS PATCH-004 - Compact navigation
# Changes only MainPage.xaml and MainPage.xaml.cs, preserving the rest of the system.
$ErrorActionPreference = 'Stop'
$root = 'E:\Private\Projekty\LiveH OS 2.0'
$patchRoot = $PSScriptRoot
$items = @(
    @{ RelativePath = "src/LiveH.App/MainPage.xaml"; OldHash = "7981626c2f81924332b8abca9deb940a7fd6f6e23f9f77c8ee5bf229568586bc"; NewHash = "942838fe42c5b146d08695b69a8907b60b2da84a228fea8b2eb0ee3eb2b650cf" },
    @{ RelativePath = "src/LiveH.App/MainPage.xaml.cs"; OldHash = "1c7a57f861515c77282c2357fe4c134e6735a2afebfde0f00f2b613e857ffe90"; NewHash = "63dfae9bec742fec63308a217bc6fa45f0e28aa15d89ad4efbe7fdf8aedfd8bb" }
)

function Hash-Of([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $root 'LiveH.sln'))) {
        throw "Nie znaleziono projektu LiveH.sln: $root"
    }
    $needsUpdate = @()
    foreach ($item in $items) {
        $dst = Join-Path $root $item.RelativePath
        $src = Join-Path (Join-Path $patchRoot 'files') $item.RelativePath
        if (-not (Test-Path -LiteralPath $dst)) { throw "Brak pliku projektu: $dst" }
        if (-not (Test-Path -LiteralPath $src)) { throw "Brak pliku poprawki: $src" }
        if ((Hash-Of $src) -ne $item.NewHash) { throw "Uszkodzony plik poprawki: $src" }
        $current = Hash-Of $dst
        if ($current -eq $item.NewHash) { continue }
        if ($current -ne $item.OldHash) {
            throw "Plik zostal zmieniony poza PATCH-004. Nie nadpisuje: $dst"
        }
        $needsUpdate += $item
    }

    if ($needsUpdate.Count -eq 0) {
        Write-Host '[LIVEH] PATCH-004 juz zainstalowany. Brak zmian.' -ForegroundColor Green
        exit 0
    }

    $backupDir = Join-Path $root ('zip\_backups\PATCH-004-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    foreach ($item in $needsUpdate) {
        $dst = Join-Path $root $item.RelativePath
        $backup = Join-Path $backupDir $item.RelativePath
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
        Copy-Item -LiteralPath $dst -Destination $backup -ErrorAction Stop
    }
    $written = @()
    try {
        foreach ($item in $needsUpdate) {
            $dst = Join-Path $root $item.RelativePath
            $src = Join-Path (Join-Path $patchRoot 'files') $item.RelativePath
            Copy-Item -LiteralPath $src -Destination $dst -Force -ErrorAction Stop
            $written += $item
        }
    }
    catch {
        foreach ($item in $written) {
            $backup = Join-Path $backupDir $item.RelativePath
            $dst = Join-Path $root $item.RelativePath
            Copy-Item -LiteralPath $backup -Destination $dst -Force
        }
        throw
    }
    Write-Host '[LIVEH] SUKCES: PATCH-004 COMPACT MENU ZAINSTALOWANY' -ForegroundColor Green
    Write-Host "[LIVEH] Backup: $backupDir" -ForegroundColor Gray
    Write-Host '[LIVEH] Visual Studio: Build > Rebuild Solution > F5' -ForegroundColor Cyan
}
catch {
    Write-Host ('[LIVEH BLAD] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
