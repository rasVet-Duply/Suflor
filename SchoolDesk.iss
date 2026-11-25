; Inno Setup 6. Сначала выполните scripts\Build.ps1, затем скомпилируйте этот файл.
; Мастер спрашивает имя учётной записи ученика и запускает scripts\Install.ps1.
[Setup]
AppName=SchoolDesk OS
AppVersion=1.0.0
DefaultDirName={autopf}\SchoolDesk
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=SchoolDesk-Setup
DisableProgramGroupPage=yes
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\dist\SchoolDesk\app\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "..\scripts\Install.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\Uninstall.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Run]
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\Install.ps1"" -Source ""{app}"" -InstallDir ""{app}"" -Student ""{code:GetStudent}"" -CreateStudent"; \
  StatusMsg: "Настройка школьного режима..."; Flags: waituntilterminated

[UninstallRun]
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\Uninstall.ps1"" -InstallDir ""{app}"" -KeepData"; \
  RunOnceId: "SchoolDeskRestore"; Flags: waituntilterminated

[Code]
var
  StudentPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  StudentPage := CreateInputQueryPage(wpSelectDir, 'Учётная запись ученика',
    'Для какой учётной записи Windows включить школьный режим?',
    'Если её нет, она будет создана (без прав администратора).');
  StudentPage.Add('Имя учётной записи:', False);
  StudentPage.Values[0] := 'Student';
end;

function GetStudent(Param: String): String;
begin
  Result := StudentPage.Values[0];
end;
