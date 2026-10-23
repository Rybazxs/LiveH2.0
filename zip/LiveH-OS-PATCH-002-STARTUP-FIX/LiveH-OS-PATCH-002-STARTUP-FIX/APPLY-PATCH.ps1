# LiveH OS 2.0 - PATCH-002 (resource initialization order)
# No external calls, no project recreation. Windows PowerShell 5.1 compatible.
$ErrorActionPreference = 'Stop'
try {
    $zipFolder = Split-Path -Parent $PSScriptRoot
    if ((Split-Path -Leaf $zipFolder) -ne 'zip') {
        throw 'Wypakuj ZIP do E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-002-STARTUP-FIX'
    }
    $root = Split-Path -Parent $zipFolder
    $relative = 'src\LiveH.App\App.xaml.cs'
    $target = Join-Path $root $relative
    $replacement = Join-Path $PSScriptRoot 'files\src\LiveH.App\App.xaml.cs'
    $solution = Join-Path $root 'LiveH.sln'
    if (-not (Test-Path -LiteralPath $solution)) { throw "Nie znaleziono LiveH.sln w $root" }
    if (-not (Test-Path -LiteralPath $target)) { throw "Nie znaleziono $target" }
    if (-not (Test-Path -LiteralPath $replacement)) { throw 'Brak pliku poprawki. Rozpakuj cala paczke ZIP.' }
    $expectedHash = '2b5a1a16ca2f572773eb01754437fb982b9e61d5f7e7a0d9237a31494ce94229'
    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $newHash = (Get-FileHash -LiteralPath $replacement -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -eq $newHash) {
        Write-Host '[LIVEH] PATCH-002 jest juz zastosowany.' -ForegroundColor Green
        exit 0
    }
    if ($actualHash -ne $expectedHash) {
        throw 'Plik App.xaml.cs jest inny niz w Foundation 0.0.1. Nie nadpisuje Twoich zmian. Zglos ten komunikat.'
    }
    $backupDir = Join-Path $root ('patches\backups\PATCH-002-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir 'App.xaml.cs') -ErrorAction Stop
    $tempPath = "$target.patch002.new"
    Copy-Item -LiteralPath $replacement -Destination $tempPath -ErrorAction Stop
    Move-Item -LiteralPath $tempPath -Destination $target -Force -ErrorAction Stop
    Write-Host '[LIVEH] PATCH-002 zastosowany. Poprzedni App.xaml.cs zapisano w:' -ForegroundColor Green
    Write-Host "  $backupDir"
    Write-Host '[LIVEH] W Visual Studio: Build > Rebuild Solution; nastepnie uruchom LiveH.App na Androidzie (F5).' -ForegroundColor Cyan
}
catch {
    Write-Host "[LIVEH BLAD] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
