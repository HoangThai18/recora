; Recora installer (Inno Setup 6). Per-user: no admin prompt, installs to %LocalAppData%\Programs\Recora.
; Built on CI by tools/ci/installer.ps1 from the staged portable folder: iscc /DVersion=x.y.z /DSource=<stage> /DOutput=<dir>

#ifndef Version
  #define Version "0.0.0"
#endif

[Setup]
AppId={{6F1C2B7E-4D3A-4B8E-9C61-2E7A0B5D9F14}
AppName=Recora
AppVersion={#Version}
AppVerName=Recora {#Version}
AppPublisher=thaipro.store
AppPublisherURL=https://thaipro.store/phan-mem/recora
AppSupportURL=https://github.com/HoangThai18/recora/issues
DefaultDirName={localappdata}\Programs\Recora
DefaultGroupName=Recora
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir={#Output}
OutputBaseFilename=recora-{#Version}-windows-x64-setup
SetupIconFile={#Icon}
UninstallDisplayIcon={app}\Recora.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
LicenseFile={#Source}\licenses\Recora-LICENSE.txt

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng ngoài màn hình (Desktop)"; GroupDescription: "Biểu tượng:"

[Files]
Source: "{#Source}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Recora"; Filename: "{app}\Recora.exe"
Name: "{group}\Gỡ Recora"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Recora"; Filename: "{app}\Recora.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Recora.exe"; Description: "Mở Recora"; Flags: nowait postinstall skipifsilent

; "Cập nhật ngay" runs this installer silently with /RELAUNCH=1: start the new Recora once it is in place.
Filename: "{app}\Recora.exe"; Flags: nowait; Check: IsRelaunch

[Code]
function IsRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM Recora.exe /F"; Flags: runhidden; RunOnceId: "StopRecora"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Registry]
; "Mở cùng Windows" lives in the per-user Run key; remove it with the app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Recora"; Flags: uninsdeletevalue dontcreatekey
