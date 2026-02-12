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
#define DotNetInstallerExe "windowsdesktop-runtime-8.0.24-win-x64.exe"
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
; .NET 8 DESKTOP RUNTIME (for prerequisite installation)
; ============================================================================
Source: "..\..\tools\dotnet\{#DotNetInstallerExe}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsDotNet8DesktopInstalled

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
// UNINSTALL PREVIOUS VERSION (MANUAL CLEANUP - NO DIALOGS)
// ============================================================================
// We bypass the old uninstaller entirely because it may show dialogs during
// silent uninstall. Instead, we manually delete files and clean up registry.
// This ensures a completely silent upgrade experience.
// ============================================================================

procedure CleanupOldRegistryEntries();
var
  UninstallKey: String;
begin
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8F9E7A2-4C3D-4F1E-9B2A-7E8D5C1F4A6B}_is1';
  
  // Delete registry entries from both HKLM and HKCU
  RegDeleteKeyIncludingSubkeys(HKLM, UninstallKey);
  RegDeleteKeyIncludingSubkeys(HKCU, UninstallKey);
  
  // Also clean up App Paths
  RegDeleteKeyIncludingSubkeys(HKA, 'Software\Microsoft\Windows\CurrentVersion\App Paths\ArdysaModsTools.exe');
end;

function UninstallOldVersion(): Boolean;
var
  InstallLocation: String;
begin
  Result := True;
  InstallLocation := GetInstallLocation();
  
  if InstallLocation <> '' then
  begin
    // First, try to close the app if running
    CloseRunningApp('ArdysaModsTools.exe');
    Sleep(500);
    
    // Delete the installation directory completely
    if DirExists(InstallLocation) then
    begin
      DeleteDirectory(InstallLocation);
      
      // Wait and verify deletion
      Sleep(500);
      if DirExists(InstallLocation) then
      begin
        // If directory still exists, try once more
        Sleep(1000);
        DeleteDirectory(InstallLocation);
      end;
    end;
    
    // The uninstaller is inside the InstallLocation, so it's already deleted
    // by DeleteDirectory above. Just clean up registry entries.
    CleanupOldRegistryEntries();
  end
  else
  begin
    // No install location found, just clean up registry
    CleanupOldRegistryEntries();
  end;
end;

// ============================================================================
// .NET 8 DESKTOP RUNTIME CHECK (Registry-based, multi-path)
// ============================================================================
// Detection uses RegGetValueNames to enumerate installed runtime versions.
// .NET stores versions as VALUE NAMES (REG_DWORD), NOT as subkeys.
// Three registry paths are checked to cover x64, WOW6432Node, and ARM64.
// ============================================================================

function CheckDotNet8InRegistryPath(const RegPath: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  
  if RegGetValueNames(HKLM, RegPath, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('8.', Names[I]) = 1 then
      begin
        Log('Found .NET 8 Desktop Runtime: ' + Names[I] + ' at ' + RegPath);
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function IsDotNet8DesktopInstalled(): Boolean;
begin
  Result := False;
  
  // Path 1: Native x64 registry path
  if CheckDotNet8InRegistryPath('SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    Result := True;
    Exit;
  end;
  
  // Path 2: WOW6432Node x64 path (32-bit process reading 64-bit registry)
  if CheckDotNet8InRegistryPath('SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    Result := True;
    Exit;
  end;
  
  // Path 3: ARM64 path (for ARM devices running x64 emulation)
  if CheckDotNet8InRegistryPath('SOFTWARE\dotnet\Setup\InstalledVersions\arm64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    Result := True;
    Exit;
  end;
  
  Log('.NET 8 Desktop Runtime NOT found in any registry path.');
end;

// ============================================================================
// .NET 8 DESKTOP RUNTIME INSTALLATION (Bundled silent installer)
// ============================================================================

function InstallDotNet8Runtime(): Boolean;
var
  ResultCode: Integer;
  InstallerPath: String;
  ErrorCode: Integer;
begin
  Result := False;
  InstallerPath := ExpandConstant('{tmp}\{#DotNetInstallerExe}');
  
  Log('Installing .NET 8 Desktop Runtime from: ' + InstallerPath);
  
  if not FileExists(InstallerPath) then
  begin
    Log('ERROR: .NET 8 installer not found at: ' + InstallerPath);
    if MsgBox('.NET 8 Desktop Runtime installer was not found.' + #13#10 + #13#10 +
              'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', '{#DotNetDownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Exit;
  end;
  
  if Exec(InstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('.NET 8 installer exited with code: ' + IntToStr(ResultCode));
    // Exit codes: 0 = success, 1641/3010 = success but reboot needed
    if (ResultCode = 0) or (ResultCode = 1641) or (ResultCode = 3010) then
    begin
      Result := True;
      Log('.NET 8 Desktop Runtime installed successfully.');
    end
    else
    begin
      Log('ERROR: .NET 8 installation failed with code: ' + IntToStr(ResultCode));
      if MsgBox('.NET 8 Desktop Runtime installation failed (Error: ' + IntToStr(ResultCode) + ').' + #13#10 + #13#10 +
                'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        ShellExec('open', '{#DotNetDownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      end;
    end;
  end
  else
  begin
    Log('ERROR: Failed to execute .NET 8 installer.');
    if MsgBox('Could not run .NET 8 Desktop Runtime installer.' + #13#10 + #13#10 +
              'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', '{#DotNetDownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
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
  // Check & Install .NET 8 Desktop Runtime
  // ----------------------------------------------------------------
  if not IsDotNet8DesktopInstalled() then
  begin
    Log('.NET 8 Desktop Runtime not detected. Attempting bundled installation...');
    if not InstallDotNet8Runtime() then
    begin
      if MsgBox('.NET 8 Desktop Runtime could not be installed.' + #13#10 + #13#10 +
                'The application may not work correctly without it.' + #13#10 +
                'Do you want to continue anyway?',
                mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
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
// UNINSTALL - SILENT DATA CLEANUP (NO DIALOGS)
// ============================================================================
// Completely silent uninstall - no confirmation dialogs.
// All settings and mod data are automatically removed on uninstall.
// ============================================================================

function InitializeUninstall(): Boolean;
begin
  // Always allow uninstall - no confirmation dialog
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // After uninstall completes, silently clean up all user data
  if CurUninstallStep = usPostUninstall then
  begin
    // Silently delete all settings and mod data - no dialog needed
    DelTree(ExpandConstant('{userappdata}\{#MyAppName}'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\{#MyAppName}'), True, True, True);
  end;
end;
