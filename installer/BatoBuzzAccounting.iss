; BatoBuzz Accounting - Inno Setup Installer Script
#define MyAppName "BatoBuzz Accounting"
#define MyAppVersion "1.0.9"
#define MyAppPublisher "BatoBuzz Technologies Pvt Ltd"
#define MyAppURL "https://batobuzz.com"
#define MyAppExeName "BatoBuzz.Desktop.exe"

[Setup]
AppId={{BatoBuzz-Accounting-2026}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
SetupIconFile=..\src\BatoBuzz.Desktop\Assets\BatoBuzz.ico
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\BatoBuzz\Accounting
DisableProgramGroupPage=no
OutputDir=..\dist
OutputBaseFilename=BatoBuzzAccounting_Setup_v{#MyAppVersion}
MinVersion=10.0
PrivilegesRequired=admin
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
