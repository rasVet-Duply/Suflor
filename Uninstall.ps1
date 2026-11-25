#Requires -RunAsAdministrator
<#
 Удаление SchoolDesk OS и возврат обычного рабочего стола.
 Пример:  .\Uninstall.ps1 -Student Student
   -KeepData  оставить настройки и журнал в ProgramData
#>
param(
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'SchoolDesk'),
    [string]$Student = '',
    [switch]$KeepData
)
$ErrorActionPreference = 'Continue'
$data = Join-Path $env:ProgramData 'SchoolDesk'

if (-not $Student) {
    try { $Student = (Get-Content "$data\settings.json" -Raw -Encoding UTF8 | ConvertFrom-Json).StudentAccount } catch { }
    if (-not $Student) { $Student = 'Student' }
}

# 1. Политики браузера и AppLocker
$exe = Join-Path $InstallDir 'SchoolDesk.exe'
if (Test-Path $exe) { Start-Process $exe -ArgumentList '--remove-policies' -Wait }

# 2. Оболочка и политики ученика
try {
    $sid = (New-Object Security.Principal.NTAccount($Student)).Translate([Security.Principal.SecurityIdentifier]).Value
    $plKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$sid"
    $loaded = Test-Path "Registry::HKEY_USERS\$sid"
    $root = "Registry::HKEY_USERS\$sid"
    if (-not $loaded -and (Test-Path $plKey)) {
        $pp = (Get-ItemProperty $plKey).ProfileImagePath
        reg load HKU\SchoolDeskTemp "$pp\NTUSER.DAT" | Out-Null
        $root = 'Registry::HKEY_USERS\SchoolDeskTemp'
    }
    Remove-ItemProperty -Path "$root\Software\Microsoft\Windows NT\CurrentVersion\Winlogon" -Name 'Shell' -ErrorAction SilentlyContinue
    $pol = "$root\Software\Microsoft\Windows\CurrentVersion\Policies\System"
    Remove-ItemProperty -Path $pol -Name 'DisableTaskMgr' -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $pol -Name 'DisableRegistryTools' -ErrorAction SilentlyContinue
    if (-not $loaded) { [gc]::Collect(); [gc]::WaitForPendingFinalizers(); reg unload HKU\SchoolDeskTemp | Out-Null }
    Write-Host "Оболочка '$Student' восстановлена (explorer.exe)."
} catch { Write-Warning "Не удалось восстановить оболочку для '$Student': $_" }

# 3. Файлы
Get-Process SchoolDesk -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1
if ((Resolve-Path $InstallDir -ErrorAction SilentlyContinue)) { Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue }
if (-not $KeepData) { Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host 'Готово. Перезагрузите компьютер.'
