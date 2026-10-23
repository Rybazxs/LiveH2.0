# LiveH OS PATCH-004C - bottom navigation frame only
$ErrorActionPreference = 'Stop'
$projectRoot = 'E:\Private\Projekty\LiveH OS 2.0'
$relative = 'src\LiveH.App\MainPage.xaml'
$expectedBefore = 'bb133b515e16db601fe12e6ee72759b10ac80415667bd4b26661bff277b35e0c'
$expectedAfter = 'c6ba10a6412d2d8f82196f77d4f5a7080294cdc8da58187ec504d73bdb5c3efb'
$source = Join-Path $PSScriptRoot ('files\' + $relative)
$target = Join-Path $projectRoot $relative
try {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'LiveH.sln'))) {
        throw 'Nie znaleziono LiveH.sln w katalogu projektu.'
    }
    if (-not (Test-Path -LiteralPath $source)) { throw "Brak pliku paczki: $source" }
    if (-not (Test-Path -LiteralPath $target)) { throw "Brak pliku projektu: $target" }
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sourceHash -ne $expectedAfter) { throw 'Niepoprawna suma SHA256 poprawki.' }
    if ($targetHash -eq $expectedAfter) {
        Write-Host '[LIVEH] PATCH-004C juz zainstalowany.' -ForegroundColor Green
        exit 0
    }
    if ($targetHash -ne $expectedBefore) {
        throw 'Niezgodna wersja MainPage.xaml. Wymagany PATCH-004B; nie nadpisuje zmian.'
    }
    $backupDir = Join-Path $projectRoot ('zip\_backups\PATCH-004C-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir 'MainPage.xaml') -ErrorAction Stop
    Copy-Item -LiteralPath $source -Destination $target -Force -ErrorAction Stop
    Write-Host '[LIVEH] SUKCES: PATCH-004C - OBRAMOWANIE DOLNEGO MENU.' -ForegroundColor Green
    Write-Host "[LIVEH] Kopia: $backupDir"
    Write-Host '[LIVEH] Rebuild Solution > F5'
}
catch {
    Write-Host ('[LIVEH BLAD] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
