# Сборка и упаковка. Требуется .NET 8 SDK на Windows.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out  = Join-Path $root 'dist\SchoolDesk'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish "$root\src\SchoolDesk.Launcher\SchoolDesk.Launcher.csproj" `
    -c Release -r win-x64 --self-contained true -o "$out\app"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Copy-Item "$root\scripts\Install.ps1","$root\scripts\Uninstall.ps1" $out
Copy-Item "$root\README.md" $out -ErrorAction SilentlyContinue

$zip = Join-Path $root 'dist\SchoolDesk-win-x64.zip'
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$out\*" -DestinationPath $zip
Write-Host "Готово: $zip"
