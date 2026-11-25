#Requires -RunAsAdministrator
<#
 Установка SchoolDesk OS.
 Пример:  .\Install.ps1 -Student Student -CreateStudent
 Параметры:
   -Source        папка с SchoolDesk.exe (по умолчанию .\app)
   -InstallDir    куда ставить (по умолчанию C:\Program Files\SchoolDesk)
   -Student       имя учётной записи ученика (без прав администратора)
   -CreateStudent создать учётку, если её нет (без пароля, только локальный вход)
   -SkipShell     не заменять оболочку (для проверки без блокировки рабочего стола)
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot 'app'),
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'SchoolDesk'),
    [string]$Student = 'Student',
    [switch]$CreateStudent,
    [switch]$SkipShell
)
$ErrorActionPreference = 'Stop'

# --- 1. Проверка совместимости ---
$os = Get-CimInstance Win32_OperatingSystem
$build = [int]$os.BuildNumber
if ($build -lt 17763) { throw "Нужна Windows 10 1809 или новее (сборка $build)." }
$edition = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').EditionID
Write-Host "Windows: $($os.Caption), сборка $build, редакция $edition"
if ($edition -like 'Core*') {
    Write-Warning 'Windows Home: AppLocker недоступен. Будут работать политики браузера, мониторинг процессов и замена оболочки.'
} elseif ($edition -like 'Professional*') {
    Write-Warning 'Windows Pro: AppLocker работает не во всех сборках; проверьте блокировку по чек-листу docs/TESTING.md.'
}

# --- 2. Копирование файлов ---
if (-not (Test-Path (Join-Path $Source 'SchoolDesk.exe'))) { throw "Не найден SchoolDesk.exe в '$Source'." }
if ((Resolve-Path $Source).Path.TrimEnd('\') -ne $InstallDir.TrimEnd('\')) {
    New-Item -ItemType Directory -Force $InstallDir | Out-Null
    robocopy $Source $InstallDir /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy завершился с ошибкой ($LASTEXITCODE)" }
}
$exe = Join-Path $InstallDir 'SchoolDesk.exe'

# --- 3. Конфигурация: ученик — только чтение, журнал — запись ---
$data = Join-Path $env:ProgramData 'SchoolDesk'
New-Item -ItemType Directory -Force $data, "$data\logs" | Out-Null
icacls $data /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
icacls "$data\logs" /grant '*S-1-5-32-545:(OI)(CI)M' | Out-Null

# --- 4. Учётная запись ученика ---
if (-not (Get-LocalUser -Name $Student -ErrorAction SilentlyContinue)) {
    if (-not $CreateStudent) { throw "Учётная запись '$Student' не найдена. Создайте её или добавьте -CreateStudent." }
    New-LocalUser -Name $Student -NoPassword -UserMayNotChangePassword -Description 'SchoolDesk student' | Out-Null
    Add-LocalGroupMember -SID 'S-1-5-32-545' -Member $Student
    Write-Host "Создана учётная запись '$Student' (без пароля)."
}
if (Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*\$Student" }) {
    Write-Warning "'$Student' входит в группу администраторов — ограничения можно обойти. Уберите её из группы."
}
$sid = (New-Object Security.Principal.NTAccount($Student)).Translate([Security.Principal.SecurityIdentifier]).Value

# --- 5. Профиль ученика (создаём, если пользователь ещё не входил) ---
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class UserProfileApi {
  [DllImport("userenv.dll", CharSet=CharSet.Unicode)]
  public static extern int CreateProfile(string pszUserSid, string pszUserName, StringBuilder pszProfilePath, uint cchProfilePath);
}
"@
$plKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$sid"
if (-not (Test-Path $plKey)) {
    $sb = New-Object Text.StringBuilder 260
    $hr = [UserProfileApi]::CreateProfile($sid, $Student, $sb, 260)
    if ($hr -ne 0 -and $hr -ne -2147024713) { throw "Не удалось создать профиль (HRESULT $hr). Войдите под '$Student' один раз и повторите." }
}

# --- 6. Первичная настройка: пароль администратора SchoolDesk ---
Write-Host 'Откроется окно настройки: задайте пароль администратора SchoolDesk.'
$p = Start-Process -FilePath $exe -ArgumentList '--setup', '--student', $Student -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Первичная настройка не завершена. Установка прервана.' }

# --- 7. Оболочка и политики для ученика (через его реестровый куст) ---
if (-not $SkipShell) {
    $profilePath = (Get-ItemProperty $plKey).ProfileImagePath
    $loaded = Test-Path "Registry::HKEY_USERS\$sid"
    $root = "Registry::HKEY_USERS\$sid"
    if (-not $loaded) {
        reg load HKU\SchoolDeskTemp "$profilePath\NTUSER.DAT" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось загрузить куст реестра ученика.' }
        $root = 'Registry::HKEY_USERS\SchoolDeskTemp'
    }
    try {
        $wl = "$root\Software\Microsoft\Windows NT\CurrentVersion\Winlogon"
        New-Item -Path $wl -Force | Out-Null
        Set-ItemProperty -Path $wl -Name 'Shell' -Value "`"$exe`""
        $pol = "$root\Software\Microsoft\Windows\CurrentVersion\Policies\System"
        New-Item -Path $pol -Force | Out-Null
        Set-ItemProperty -Path $pol -Name 'DisableTaskMgr' -Value 1 -Type DWord
        Set-ItemProperty -Path $pol -Name 'DisableRegistryTools' -Value 1 -Type DWord
    } finally {
        if (-not $loaded) { [gc]::Collect(); [gc]::WaitForPendingFinalizers(); reg unload HKU\SchoolDeskTemp | Out-Null }
    }
    Write-Host "Оболочка ученика '$Student' = SchoolDesk."
}

Write-Host ''
Write-Host 'Готово. Выйдите из системы и войдите под учётной записью ученика.'
Write-Host 'Авто-вход (по желанию): Sysinternals Autologon (пароль хранится в LSA, а не в открытом виде).'
Write-Host "Восстановление: $PSScriptRoot\Uninstall.ps1 (см. README, раздел «Восстановление доступа»)."
