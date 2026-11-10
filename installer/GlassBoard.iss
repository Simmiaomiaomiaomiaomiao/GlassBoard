#define AppName "GlassBoard"
#define AppVersion "1.0.0-preview.1"
#define AppExeName "GlassBoard.exe"

[Setup]
AppId={{BD9CEAC1-B28E-4EA1-BB3A-9E7B0DF11C10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=GlassBoard Community Build
DefaultDirName={autopf}\GlassBoard
DefaultGroupName={#AppName}
OutputDir=..\artifacts
OutputBaseFilename=GlassBoard-Setup
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}
SetupLogging=yes

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Icons]
Name: "{group}\GlassBoard"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\GlassBoard"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch GlassBoard"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\GlassBoard.exe"; Parameters: "--restore-shell"; Flags: runhidden