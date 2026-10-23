# LiveH OS 2.0 PATCH-003: Android theme crash fix
# Only src/LiveH.App/Platforms/Android/Resources/values/styles.xml is modified.
# Designed for Explorer "Wyodrebnij wszystkie" -> double package directory.
$ErrorActionPreference = 'Stop'
try {
    $package = 'LiveH-OS-PATCH-003-ANDROID-THEME-FIX'
    $innerName = Split-Path -Leaf $PSScriptRoot
    $outerDir = Split-Path -Parent $PSScriptRoot
    $outerName = Split-Path -Leaf $outerDir
    $zipDir = Split-Path -Parent $outerDir
    if ($innerName -ne $package -or $outerName -ne $package -or (Split-Path -Leaf $zipDir) -ne 'zip') {
        throw 'Nieprawidlowa struktura folderow. Oczekiwane: zip\NAZWA-PACZKI\NAZWA-PACZKI\APPLY-PATCH.ps1'
    }
    $projectRoot = Split-Path -Parent $zipDir
    $solution = Join-Path $projectRoot 'LiveH.sln'
    $relative = 'src\LiveH.App\Platforms\Android\Resources\values\styles.xml'
    $target = Join-Path $projectRoot $relative
    $source = Join-Path $PSScriptRoot ('files\' + $relative)
    if (!(Test-Path -LiteralPath $solution -PathType Leaf)) { throw ('Nie znaleziono projektu: ' + $solution) }
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw ('Nie znaleziono pliku: ' + $target) }
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Brak pliku poprawki: ' + $source) }
    $expectedOldHash = '0a03227d0ce4a8104ed2e8bdd25fb8f2144290999a5e29b4367f87b57dd7a6f2'
    $expectedNewHash = '0eba4dc5d2533cc866ac47254599282686eccdad5918f33012d18da9f711f9e4'
    $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $patchHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($patchHash -ne $expectedNewHash) { throw 'Uszkodzona paczka: niepoprawny SHA256 pliku poprawki.' }
    if ($actualHash -eq $expectedNewHash) {
        Write-Host '[LIVEH] PATCH-003 juz zastosowany. Nic nie zmieniam.' -ForegroundColor Green
        exit 0
    }
    if ($actualHash -ne $expectedOldHash) {
        throw 'Plik styles.xml jest inny niz w Foundation 0.0.1. Nie nadpisuje zmian. Przeslij ten komunikat.'
    }
    $backupDir = Join-Path $projectRoot ('patches\backups\PATCH-003-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir 'styles.xml') -ErrorAction Stop
    $temp = $target + '.patch003.new'
    Copy-Item -LiteralPath $source -Destination $temp -ErrorAction Stop
    Move-Item -LiteralPath $temp -Destination $target -Force -ErrorAction Stop
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedNewHash) {
        throw 'Weryfikacja po instalacji nie powiodla sie.'
    }
    Write-Host '[LIVEH] SUKCES: PATCH-003 ZAINSTALOWANY.' -ForegroundColor Green
    Write-Host ('[LIVEH] Kopia poprzedniego styles.xml: ' + $backupDir) -ForegroundColor DarkGray
    Write-Host '[LIVEH] W Visual Studio: Build > Rebuild Solution, potem F5.' -ForegroundColor Cyan
}
catch {
    Write-Host ('[LIVEH BLAD] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
