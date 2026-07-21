; BatoBuzz Accounting - Windows 7 SP1 Legacy Installer
#define MyAppName "BatoBuzz Accounting (Windows 7 Legacy)"
#define MyAppVersion "1.0.10"
#define MyAppPublisher "BatoBuzz Technologies Pvt Ltd"
#define MyAppURL "https://batobuzz.com"
#define MyAppExeName "BatoBuzz.Desktop.exe"

[Setup]
AppId={{BatoBuzz-Accounting-Windows7-2026}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
SetupIconFile=..\src\BatoBuzz.Desktop\Assets\BatoBuzz.ico
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\BatoBuzz\Accounting
OutputDir=..\dist
OutputBaseFilename=BatoBuzzAccounting_Windows7_Legacy_Setup_v{#MyAppVersion}
MinVersion=6.1sp1
PrivilegesRequired=admin
Compression=lzma
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish-win7\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
