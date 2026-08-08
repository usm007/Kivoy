#define MyAppName "Kivoy"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "usm007"
#define MyAppExeName "Kivoy.exe"

[Setup]
AppId={{AC333111-A749-43BB-A72F-AF0F47DB42F4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Kivoy
DefaultGroupName=Kivoy
DisableProgramGroupPage=yes
OutputDir=Setup
OutputBaseFilename=KivoySetup-{#MyAppVersion}-Lite
SetupIconFile=..\src\Kivoy\Assets\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\src\Kivoy\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "redist\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: ignoreversion
Source: "install-engines.ps1"; DestDir: "{tmp}"; Flags: ignoreversion

[Icons]
Name: "{group}\Kivoy"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Kivoy"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Kivoy"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Kivoy}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2Client = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function IsWebView2Installed: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WebView2Client) or
            RegKeyExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2Client);
end;

procedure InstallWebView2;
var
  TmpFile: String;
  ResultCode: Integer;
begin
  TmpFile := ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe');
  if not FileExists(TmpFile) then
    Exit;
  if Exec(TmpFile, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('WebView2 installer exit code: ' + IntToStr(ResultCode));
end;

procedure RunEngineSetup;
var
  ScriptPath: String;
  PS: String;
  ResultCode: Integer;
begin
  ScriptPath := ExpandConstant('{tmp}\install-engines.ps1');
  if not FileExists(ScriptPath) then
    Exit;
  PS := '-NoProfile -ExecutionPolicy Bypass -File "' + ScriptPath + '"';
  Log('Running engine setup script...');
  if Exec('powershell.exe', PS, '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Engine setup exit code: ' + IntToStr(ResultCode));
    if ResultCode <> 0 then
      MsgBox('Some download engines could not be installed. Kivoy will download them automatically on first run instead.', mbInformation, MB_OK);
  end
  else
  begin
    MsgBox('Could not run the engine setup. Kivoy will download the engines on first run instead.', mbInformation, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not IsWebView2Installed() then
      InstallWebView2;
    RunEngineSetup;
  end;
end;
