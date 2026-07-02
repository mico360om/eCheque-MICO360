; Inno Setup script for eCheque MICO360 (Windows installer)
; Build:  iscc /DMyAppVersion=1.0.1 installer\eCheque.MICO360.iss
; Expects a self-contained publish in ..\publish (see the release workflow).

#define MyAppName "eCheque MICO360"
#define MyAppPublisher "MICO360 Softwares"
#define MyAppExeName "eCheque.MICO360.exe"
#define MyAppURL "https://github.com/mico360om/eCheque-MICO360"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{9B2C7F1E-4D3A-4F62-9E77-eChequeMICO360}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\eCheque MICO360
DefaultGroupName=eCheque MICO360
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=eCheque-MICO360-Setup-{#MyAppVersion}
OutputDir=..\installer_output
SetupIconFile=..\logo.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Machine-wide install into Program Files (requires admin / UAC).
PrivilegesRequired=admin
; Close a running instance during an update so files aren't locked.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\eCheque MICO360"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall eCheque MICO360"; Filename: "{uninstallexe}"
Name: "{autodesktop}\eCheque MICO360"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,eCheque MICO360}"; Flags: nowait postinstall skipifsilent
