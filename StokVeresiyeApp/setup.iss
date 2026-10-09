; Inno Setup Script - Bilensis
#define MyAppName "Bilensis"
#define MyAppVersion "2.8.0"
#define MyAppPublisher "Bilensis"
#define MyAppExeName "StokVeresiyeApp.exe"

[Setup]
AppId={{E68A9F1A-B472-4B1C-9E5F-8F8D69A3C721}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Bilensis
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\Setup_Output
OutputBaseFilename=Bilensis_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=Resources\app.ico
WizardSmallImageFile=Resources\wizard_small.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}

; Güncelleme / Update Yapılandırması:
; Karşı PC'de önceki kurulum varsa yolu otomatik algılar ve dizin sormadan direkt günceller
UsePreviousAppDir=yes
DirExistsWarning=no
DisableDirPage=auto
DisableProgramGroupPage=auto
CloseApplications=yes
RestartApplications=no
AlwaysRestart=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Masaüstü simgesi güncellemelerde varsayılan olarak seçili gelir
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Karşı bilgisayarda açık olan eski program varsa dosya kilitlenmesini önlemek için kurulum başlamadan kapat
procedure KillProcess(const ExeName: string);
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im ' + ExeName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    KillProcess('StokVeresiyeApp.exe');
    Sleep(500);
  end;
end;

