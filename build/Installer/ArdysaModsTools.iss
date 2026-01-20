; ============================================================================
; ArdysaModsTools (AMT 2.0) - Professional Installer
; ============================================================================
; Modern Inno Setup installer with enhanced UI/UX, prerequisite checking,
; automated updates, and intelligent installation logic.
; ============================================================================

#define MyAppName "ArdysaModsTools"
#define MyAppVersion "2.0"
#define MyAppVersionNumeric "2.0.0.0"
#define MyAppPublisher "Ardysa"
#define MyAppURL "https://github.com/Anneardysa/ArdysaModsTools"
#define MyAppExeName "ArdysaModsTools.exe"
#define DotNetDownloadUrl "https://dotnet.microsoft.com/download/dotnet/8.0"
#define WebView2DownloadUrl "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"

[Setup]
; ============================================================================
; APPLICATION IDENTITY
; ============================================================================
AppId={{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; ============================================================================
; INSTALLATION SETTINGS
; ============================================================================
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
LicenseFile=..\..\LICENSE
InfoBeforeFile=..\..\docs\INSTALL_INFO.txt

; ============================================================================
; OUTPUT SETTINGS
; ============================================================================
OutputDir=Output
OutputBaseFilename=ArdysaModsTools_Setup_x64
SetupIconFile=..\..\Assets\Icons\AppIcon.ico

; ============================================================================
; ARCHITECTURE (64-bit only)
; ============================================================================
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; ============================================================================
; COMPRESSION (Maximum)
; ============================================================================
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; ============================================================================
; MODERN UI CONFIGURATION
; ============================================================================
WizardStyle=modern
WizardResizable=no
WizardSizePercent=110
DisableWelcomePage=no
DisableReadyPage=no
AlwaysShowDirOnReadyPage=yes
ShowLanguageDialog=auto

; ============================================================================
; VERSION INFO
; ============================================================================
VersionInfoVersion={#MyAppVersionNumeric}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=The Ultimate Dota 2 Mod Manager
VersionInfoCopyright=© 2024-2026 Ardysa
MinVersion=10.0.17763

; ============================================================================
; UNINSTALL SETTINGS
; ============================================================================
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.AppDescription=The Ultimate Dota 2 Mod Manager
english.LaunchProgram=Launch {#MyAppName}
english.CreateDesktopIcon=Create a &desktop shortcut

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
; ============================================================================
; MAIN APPLICATION FILES
; ============================================================================
Source: "..\..\publish\installer\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\publish\installer\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\..\publish\installer\*.json"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\..\publish\installer\*.xml"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; ============================================================================
; ASSETS
; ============================================================================
Source: "..\..\publish\installer\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs

; ============================================================================
; FONTS (JetBrains Mono)
; ============================================================================
Source: "..\..\Assets\Fonts\JetBrainsMono-Regular.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\..\Assets\Fonts\JetBrainsMono-Bold.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono Bold"; Flags: onlyifdoesntexist uninsneveruninstall

; ============================================================================
; EXTERNAL TOOLS (must be in app root for code compatibility)
; ============================================================================
Source: "..\..\tools\hllib\HLExtract.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\tools\hllib\HLLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\tools\vpk\vpk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\tools\vpk\*.dll"; DestDir: "{app}"; Flags: ignoreversion

; ============================================================================
; WEBVIEW2 RUNTIME (for prerequisite installation)
; ============================================================================
Source: "..\..\tools\webview2\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsWebView2Installed

; ============================================================================
; DOCUMENTATION
; ============================================================================
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; ============================================================================
; SHORTCUTS
; ============================================================================
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{cm:AppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{cm:AppDescription}"

[Registry]
; ============================================================================
; APP PATHS (Windows integration)
; ============================================================================
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"

[Run]
; ============================================================================
; POST-INSTALLATION
; ============================================================================
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent shellexec unchecked

[UninstallDelete]
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\*.tmp"

[Code]
// ============================================================================
// PASCAL SCRIPT - PREREQUISITE CHECKS & INSTALLATION LOGIC
// ============================================================================

var
  DotNetMissing: Boolean;
  WebView2Missing: Boolean;

// ============================================================================
// RUNNING APPLICATION CHECK
// ============================================================================

function IsAppRunning(const AppName: String): Boolean;
var
  ResultCode: Integer;
begin
  // Use tasklist to check if the process is running
  Result := Exec('cmd.exe', '/c tasklist /FI "IMAGENAME eq ' + AppName + '" | find /I "' + AppName + '"',
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function CloseRunningApp(const AppName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  // Use taskkill to terminate the process
  if not Exec('cmd.exe', '/c taskkill /F /IM "' + AppName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := False;
  // Wait a moment for the process to fully terminate
  Sleep(1000);
end;

// ============================================================================
// REGISTRY HELPERS
// ============================================================================

function GetUninstallString(): String;
var
  UninstallKey: String;
  UninstallString: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}_is1';
  
  if RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', UninstallString) then
    Result := UninstallString
  else if RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallString) then
    Result := UninstallString;
end;

function IsOldVersionInstalled(): Boolean;
begin
  Result := GetUninstallString() <> '';
end;

function GetInstallLocation(): String;
var
  UninstallKey: String;
  InstallLocation: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}_is1';
  
  if RegQueryStringValue(HKLM, UninstallKey, 'InstallLocation', InstallLocation) then
    Result := InstallLocation
  else if RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', InstallLocation) then
    Result := InstallLocation;
end;

// ============================================================================
// DIRECTORY CLEANUP
// ============================================================================

procedure DeleteDirectory(const DirPath: String);
var
  FindRec: TFindRec;
  FilePath: String;
begin
  if FindFirst(DirPath + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          FilePath := DirPath + '\' + FindRec.Name;
          if FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
            DeleteDirectory(FilePath)
          else
            DeleteFile(FilePath);
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
  RemoveDir(DirPath);
end;

// ============================================================================
// UNINSTALL PREVIOUS VERSION
// ============================================================================

function UninstallOldVersion(): Boolean;
var
  UninstallString: String;
  InstallLocation: String;
  ResultCode: Integer;
begin
  Result := True;
  UninstallString := GetUninstallString();
  InstallLocation := GetInstallLocation();
  
  if UninstallString <> '' then
  begin
    if (Length(UninstallString) > 0) and (UninstallString[1] = '"') then
      UninstallString := RemoveQuotes(UninstallString);
    
    if not Exec(UninstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := False;
    end;
    
    if Result and (InstallLocation <> '') then
    begin
      Sleep(1000);
      if DirExists(InstallLocation) then
        DeleteDirectory(InstallLocation);
    end;
  end;
end;

// ============================================================================
// .NET 8 DESKTOP RUNTIME CHECK
// ============================================================================

function IsDotNet8DesktopInstalled(): Boolean;
var
  Output: AnsiString;
  ExitCode: Integer;
  TempFile: String;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_check.txt');
  
  if Exec('cmd.exe', '/c dotnet --list-runtimes > "' + TempFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    if ExitCode = 0 then
    begin
      if LoadStringFromFile(TempFile, Output) then
      begin
        if Pos('Microsoft.WindowsDesktop.App 8.', String(Output)) > 0 then
          Result := True;
      end;
    end;
  end;
  
  DeleteFile(TempFile);
end;

// ============================================================================
// WEBVIEW2 RUNTIME CHECK
// ============================================================================

function IsWebView2Installed(): Boolean;
var
  Version: String;
begin
  Result := False;
  
  // Check 64-bit registry
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
  begin
    if Version <> '' then
      Result := True;
  end
  // Check 32-bit registry
  else if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
  begin
    if Version <> '' then
      Result := True;
  end
  // Check per-user installation
  else if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
  begin
    if Version <> '' then
      Result := True;
  end;
end;

// ============================================================================
// WEBVIEW2 INSTALLATION
// ============================================================================

function InstallWebView2(): Boolean;
var
  ResultCode: Integer;
  ErrorCode: Integer;
begin
  Result := False;
  
  if Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
      Result := True
    else
    begin
      if MsgBox('WebView2 Runtime installation failed (Error: ' + IntToStr(ResultCode) + ').' + #13#10 + #13#10 +
                'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        ShellExec('open', '{#WebView2DownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      end;
    end;
  end
  else
  begin
    if MsgBox('Could not run WebView2 installer.' + #13#10 + #13#10 +
              'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', '{#WebView2DownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;

// ============================================================================
// MAIN INITIALIZATION
// ============================================================================

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // ----------------------------------------------------------------
  // Close running application if detected
  // ----------------------------------------------------------------
  if IsAppRunning('{#MyAppExeName}') then
  begin
    if MsgBox('{#MyAppName} is currently running.' + #13#10 + #13#10 +
              'The application must be closed before installation can continue.' + #13#10 +
              'Do you want to close it now?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      if not CloseRunningApp('{#MyAppExeName}') then
      begin
        MsgBox('Failed to close {#MyAppName}. Please close it manually and try again.',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end
    else
    begin
      MsgBox('Installation cancelled. Please close {#MyAppName} and try again.',
             mbInformation, MB_OK);
      Result := False;
      Exit;
    end;
  end;
  
  // ----------------------------------------------------------------
  // Check .NET 8 Desktop Runtime
  // ----------------------------------------------------------------
  if not IsDotNet8DesktopInstalled() then
  begin
    if MsgBox('{#MyAppName} requires .NET 8 Desktop Runtime to run.' + #13#10 + #13#10 +
              'Would you like to download it now?' + #13#10 + #13#10 +
              'Click Yes to open the download page, then run this installer again.',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', '{#DotNetDownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    
    MsgBox('Please install .NET 8 Desktop Runtime and run this installer again.', mbInformation, MB_OK);
    Result := False;
    Exit;
  end;
  
  // ----------------------------------------------------------------
  // Check WebView2 Runtime
  // ----------------------------------------------------------------
  if not IsWebView2Installed() then
  begin
    if MsgBox('{#MyAppName} requires Microsoft Edge WebView2 Runtime.' + #13#10 + #13#10 +
              'WebView2 will be installed automatically. Continue?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      if not InstallWebView2() then
      begin
        if MsgBox('WebView2 installation may have failed. Continue anyway?' + #13#10 + #13#10 +
                  '(The application may not work correctly without WebView2)',
                  mbConfirmation, MB_YESNO) = IDNO then
        begin
          Result := False;
          Exit;
        end;
      end;
    end
    else
    begin
      Result := False;
      Exit;
    end;
  end;
  
  // ----------------------------------------------------------------
  // Uninstall Previous Version
  // ----------------------------------------------------------------
  if IsOldVersionInstalled() then
  begin
    if MsgBox('A previous version of {#MyAppName} is installed.' + #13#10 + #13#10 +
              'The old version will be removed before installing the new version.' + #13#10 +
              'Do you want to continue?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      if not UninstallOldVersion() then
      begin
        MsgBox('Failed to uninstall the previous version. Please uninstall it manually and try again.',
               mbError, MB_OK);
        Result := False;
        Exit;
      end;
    end
    else
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// ============================================================================
// WIZARD PAGE CUSTOMIZATION
// ============================================================================

procedure InitializeWizard();
begin
  // Customize welcome text
  WizardForm.WelcomeLabel2.Caption := 
    'This will install {#MyAppName} {#MyAppVersion} on your computer.' + #13#10 + #13#10 +
    '{#MyAppName} is the ultimate Dota 2 mod manager, designed to make ' +
    'installing and managing cosmetic mods effortless.' + #13#10 + #13#10 +
    'Features:' + #13#10 +
    '• One-click mod installation' + #13#10 +
    '• Custom hero sets' + #13#10 +
    '• Weather, terrain, and HUD mods' + #13#10 +
    '• Automatic patching after Dota 2 updates' + #13#10 + #13#10 +
    'Click Next to continue, or Cancel to exit.';
end;

// ============================================================================
// UNINSTALL CONFIRMATION
// ============================================================================

function InitializeUninstall(): Boolean;
begin
  Result := MsgBox('Are you sure you want to completely remove {#MyAppName}?',
                   mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask about removing user data
    if MsgBox('Do you want to remove all saved settings and mod data?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\{#MyAppName}'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\{#MyAppName}'), True, True, True);
    end;
  end;
end;
