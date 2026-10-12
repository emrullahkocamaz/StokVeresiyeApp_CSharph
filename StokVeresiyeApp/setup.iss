; Inno Setup Script - Bilensis
#define MyAppName "Bilensis"
#define MyAppVersion "2.9.1"
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
; SQL Server LocalDB (Microsoft yeniden dağıtılabilir paketi): yalnızca bilgisayarda LocalDB yoksa geçici klasöre açılır
Source: "Redist\SqlLocalDB.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsLocalDbInstalled

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Program veritabanı olarak SQL Server LocalDB kullanır; yoksa önce o kurulur (yönetici izni istenir)
Filename: "msiexec.exe"; Parameters: "/i ""{tmp}\SqlLocalDB.msi"" /qn IACCEPTSQLLOCALDBLICENSETERMS=YES"; Verb: "runas"; Flags: shellexec waituntilterminated; StatusMsg: "SQL Server LocalDB veritabanı motoru kuruluyor (birkaç dakika sürebilir)..."; Check: not IsLocalDbInstalled
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// SQL Server LocalDB herhangi bir sürümü kurulu mu? (Installed Versions altında en az bir sürüm anahtarı)
function IsLocalDbInstalled: Boolean;
var
  Versions: TArrayOfString;
begin
  Result :=
    (RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions', Versions) and (GetArrayLength(Versions) > 0)) or
    (RegGetSubkeyNames(HKLM32, 'SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions', Versions) and (GetArrayLength(Versions) > 0));
end;

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

