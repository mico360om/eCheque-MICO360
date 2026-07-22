; Inno Setup script for the eCheque MICO360 Sync Server (Windows GUI installer).
; Build:  iscc /DMyAppVersion=1.1.0 installer\eCheque.MICO360.Server.iss
; Expects the published server EXE at ..\dist\server\eCheque.MICO360.Server.exe

#define AppName "eCheque MICO360 Sync Server"
#define AppExe "eCheque.MICO360.Server.exe"
#define SvcName "eChequeSync"
#define AppPublisher "MICO360 Softwares"
#ifndef MyAppVersion
  #define MyAppVersion "1.2.4"
#endif

[Setup]
AppId={{7F4B1E92-3C2A-4D51-9F88-eChequeServer}}
AppName={#AppName}
AppVersion={#MyAppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\eCheque MICO360 Server
DisableProgramGroupPage=yes
OutputBaseFilename=eCheque-MICO360-Server-Setup-{#MyAppVersion}
OutputDir=.
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\logo.ico
UninstallDisplayIcon={app}\{#AppExe}
; Installs a Windows service -> requires administrator.
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\dist\server\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Code]
var
  CfgPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  CfgPage := CreateInputQueryPage(wpSelectDir,
    'Server Configuration',
    'Set the port and (recommended) an enrollment secret.',
    'Clients connect by URL (http://<this-server>:<port>). The enrollment secret is required from each PC the first time it connects — leave it blank for open enrollment (not recommended on a public IP).');
  CfgPage.Add('Port:', False);
  CfgPage.Add('Enrollment secret (recommended):', False);
  CfgPage.Values[0] := '5210';
end;

function PortValue: String;
begin
  Result := Trim(CfgPage.Values[0]);
  if Result = '' then Result := '5210';
end;

function SecretValue: String;
begin
  Result := Trim(CfgPage.Values[1]);
end;

{ Escapes backslashes and double-quotes for safe embedding in a JSON string. }
function JsonEscape(S: String): String;
begin
  StringChangeEx(S, '\', '\\', True);
  StringChangeEx(S, '"', '\"', True);
  Result := S;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  JsonStr, Port, ExePath, DbPath: String;
begin
  if CurStep <> ssPostInstall then Exit;

  Port := PortValue;
  ExePath := ExpandConstant('{app}\{#AppExe}');

  { Data directory in ProgramData (writable by the LocalSystem service account). }
  ForceDirectories(ExpandConstant('{commonappdata}\eCheque MICO360 Server'));

  { JSON needs backslashes doubled; StringChangeEx edits the string in place. }
  DbPath := ExpandConstant('{commonappdata}\eCheque MICO360 Server\server.db');
  StringChangeEx(DbPath, '\', '\\', True);

  { Config file next to the EXE — the server reads this at startup. }
  JsonStr :=
    '{' + #13#10 +
    '  "ECHEQUE_SERVER_DB": "' + DbPath + '",' + #13#10 +
    '  "ECHEQUE_REGISTER_SECRET": "' + JsonEscape(SecretValue) + '",' + #13#10 +
    '  "Urls": "http://0.0.0.0:' + Port + '"' + #13#10 +
    '}';
  SaveStringToFile(ExpandConstant('{app}\echeque.server.json'), JsonStr, False);

  { Firewall rule for the chosen port. }
  Exec('netsh',
    'advfirewall firewall add rule name="eCheque Sync" dir=in action=allow protocol=TCP localport=' + Port,
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  { (Re)create the Windows service, set recovery, and start it. }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#SvcName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#SvcName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'),
    'create {#SvcName} binPath= "' + ExePath + '" start= auto DisplayName= "{#AppName}"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'),
    'description {#SvcName} "Central data-sync server for eCheque MICO360."',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'),
    'failure {#SvcName} reset= 86400 actions= restart/5000/restart/5000/restart/5000',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'start {#SvcName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep <> usUninstall then Exit;
  { Stop + remove the service before files are deleted so the EXE isn't locked. }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#SvcName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#SvcName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('netsh', 'advfirewall firewall delete rule name="eCheque Sync"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
