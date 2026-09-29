#ifndef AppVersion
  #error AppVersion must be supplied by TaskbarFetch-Setup-Build.cmd.
#endif
#ifndef AppFileVersion
  #error AppFileVersion must be supplied by TaskbarFetch-Setup-Build.cmd.
#endif
#ifndef AppPortableFileName
  #error AppPortableFileName must be supplied by TaskbarFetch-Setup-Build.cmd.
#endif

#define MyAppName "TaskbarFetch"
#define MyAppPublisher "Thyann Seng"
#define MyAppURL "https://github.com/ThyannSeng/TaskbarFetch"
#define MyAppExeName "TaskbarFetch.exe"

[Setup]
AppId={{B93C7218-3A0F-4D52-86F1-E0A8454A2A16}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppComments=Created and maintained by Thyann Seng.
AppCopyright=Copyright (c) 2026 Thyann Seng
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=TaskbarFetch Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#AppFileVersion}
VersionInfoProductTextVersion={#AppVersion}
VersionInfoVersion={#AppFileVersion}
VersionInfoTextVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\TaskbarFetch
DefaultGroupName=TaskbarFetch
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
AppMutex=Local\TaskbarFetch.Singleton.v1
CloseApplications=no
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=no
LicenseFile=..\LICENSE
SetupIconFile=..\assets\TaskbarFetch.ico
UninstallDisplayIcon={app}\TaskbarFetch.exe
UninstallDisplayName=TaskbarFetch {#AppVersion}
OutputDir=..
OutputBaseFilename=TaskbarFetch-Setup-v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
MinVersion=10.0.22000

[Tasks]
Name: "startup"; Description: "Start TaskbarFetch automatically when I sign in"; GroupDescription: "Startup options:"; Flags: unchecked

[Files]
Source: "..\{#AppPortableFileName}"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\TaskbarFetch"; Filename: "{app}\TaskbarFetch.exe"; WorkingDir: "{app}"
Name: "{code:GetProjectRoot}\TaskbarFetch"; Filename: "{app}\TaskbarFetch.exe"; WorkingDir: "{app}"; Check: ShouldCreateProjectShortcut

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TaskbarFetch"; ValueData: """{app}\TaskbarFetch.exe"""; Check: ShouldRegisterStartupEntry

[Run]
Filename: "{app}\TaskbarFetch.exe"; Description: "Launch TaskbarFetch"; Flags: postinstall nowait skipifsilent

[Code]
const
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValueName = 'TaskbarFetch';

function IsManagedStartupEntry(const EntryValue: String): Boolean;
var
  EntryPath: String;
begin
  EntryPath := RemoveQuotes(Trim(EntryValue));
  Result :=
    SameText(EntryPath, ExpandConstant('{app}\TaskbarFetch.exe')) or
    SameText(EntryPath, ExpandConstant('{localappdata}\Programs\TaskbarFetch\TaskbarFetch.exe')) or
    SameText(EntryPath, ExpandConstant('{localappdata}\TaskbarFetch\TaskbarFetch.exe'));
end;

function ShouldRegisterStartupEntry: Boolean;
var
  ExistingValue: String;
begin
  if RegQueryStringValue(HKEY_CURRENT_USER, RunKeyPath, StartupValueName, ExistingValue) then
    Result := IsManagedStartupEntry(ExistingValue) or WizardIsTaskSelected('startup')
  else
    Result := WizardIsTaskSelected('startup');
end;

function GetProjectRoot(Param: String): String;
var
  SetupDirectory: String;
begin
  Result := '';
  SetupDirectory := ExtractFileDir(ExpandConstant('{srcexe}'));
  if FileExists(SetupDirectory + '\TaskbarFetch.csproj') and
     FileExists(SetupDirectory + '\{#AppPortableFileName}') then
    Result := SetupDirectory;
end;

function ShouldCreateProjectShortcut: Boolean;
var
  ShortcutPath: String;
  ProjectRoot: String;
  ExistingTarget: String;
  ShellObject: Variant;
  ShortcutObject: Variant;
begin
  Result := False;
  ProjectRoot := GetProjectRoot('');
  if ProjectRoot = '' then
    Exit;

  ShortcutPath := ProjectRoot + '\TaskbarFetch.lnk';
  if not FileExists(ShortcutPath) then
  begin
    Result := True;
    Exit;
  end;

  try
    ShellObject := CreateOleObject('WScript.Shell');
    ShortcutObject := ShellObject.CreateShortcut(ShortcutPath);
    ExistingTarget := ShortcutObject.TargetPath;
    ExistingTarget := Trim(ExistingTarget);
    Result :=
      SameText(ExistingTarget, ExpandConstant('{app}\TaskbarFetch.exe')) or
      SameText(ExistingTarget, ProjectRoot + '\{#AppPortableFileName}') or
      SameText(ExistingTarget, ProjectRoot + '\TaskbarFetch.exe') or
      SameText(ExistingTarget, ExpandConstant('{localappdata}\Programs\TaskbarFetch\TaskbarFetch.exe')) or
      SameText(ExistingTarget, ExpandConstant('{localappdata}\TaskbarFetch\TaskbarFetch.exe'));
  except
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExistingValue: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegQueryStringValue(HKEY_CURRENT_USER, RunKeyPath, StartupValueName, ExistingValue) and
       IsManagedStartupEntry(ExistingValue) then
      RegDeleteValue(HKEY_CURRENT_USER, RunKeyPath, StartupValueName);
  end;
end;
