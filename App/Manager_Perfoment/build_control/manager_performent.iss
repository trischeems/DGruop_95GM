; ============================================================================
;  DGroup - manager_performent.iss
;  Script Inno Setup 6 cho app client Manager_Perfoment (installer Windows).
;  Thuong duoc bien dich boi build_wd_manager_performent.bat (truyen AppVersion,
;  PublishDir qua /D...). Van mo truc tiep duoc bang Inno Setup IDE: cac gia tri
;  trong #ifndef ben duoi la mac dinh.
;
;  Cai vao : C:\Program Files\Dgroup\App\Manager_performent   (theo CLAUDE.md)
;  Du lieu runtime cua nguoi dung: %LOCALAPPDATA%\Dgroup\App\Manager_performent
;  -- installer/uninstaller KHONG dung toi (giu user-config, cache, logs).
; ============================================================================

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "out\win-x64\publish"
#endif

#define AppName  "Manager_Perfoment"
#define AppTitle "DGroup - Quan ly san xuat"
#define Vendor   "Dgroup"
#define ExeName  "DGroup.App.ManagerPerformance.exe"
#define IconFile "..\Assets\avalonia-logo.ico"

[Setup]
; AppId co dinh de update/go cai dat nhan dien dung app -- KHONG duoc doi
AppId={{7B3C9A15-4E2D-4F8B-9C60-D1A57E24B8F3}
AppName={#AppTitle}
AppVersion={#AppVersion}
AppVerName={#AppTitle} {#AppVersion}
AppPublisher={#Vendor}
DefaultDirName={commonpf}\{#Vendor}\App\Manager_performent
DefaultGroupName={#Vendor}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
OutputDir=out\installer
OutputBaseFilename=Setup_{#AppName}_{#AppVersion}_win-x64
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#ExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Tu dong yeu cau dong app dang chay khi cai ban update
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Tao bieu tuong ngoai Desktop"; GroupDescription: "Tuy chon them:"

[Files]
; Toan bo ban publish self-contained (da gom config.json nam canh binary)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
; Icon dung cho shortcut
Source: "{#IconFile}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppTitle}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\avalonia-logo.ico"
Name: "{autodesktop}\{#AppTitle}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\avalonia-logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "Mo {#AppTitle} ngay"; Flags: nowait postinstall skipifsilent
