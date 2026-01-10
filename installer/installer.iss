; ArdysaModsTools Inno Setup Script
; Creates a professional installer from the protected build

#define MyAppName "ArdysaModsTools"
#define MyAppVersion "2.0"
#define MyAppPublisher "Ardysa"
#define MyAppURL "https://github.com/ardysa"
#define MyAppExeName "ArdysaModsTools.exe"
#define DotNetDownloadUrl "https://dotnet.microsoft.com/download/dotnet/8.0"
#define WebView2DownloadUrl "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"

[Setup]
; Basic info
AppId={{8A7B3E5C-4D2F-4A1B-9C8E-5F6D7E8A9B0C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=..\installer_output
OutputBaseFilename=ArdysaModsTools_Setup_x64

; 64-bit only
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

SetupIconFile=..\AppIcon.ico

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; UI
WizardStyle=modern
WizardResizable=no

; Privileges - require admin for Dota 2 game file modifications
PrivilegesRequired=admin

; Uninstall
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application files from Build folder
Source: "..\Build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; JetBrains Mono fonts - install to Windows Fonts folder
Source: "..\Assets\Fonts\JetBrainsMono-Regular.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\Assets\Fonts\JetBrainsMono-Bold.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono Bold"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\Assets\Fonts\JetBrainsMono-Italic.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono Italic"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\Assets\Fonts\JetBrainsMono-BoldItalic.ttf"; DestDir: "{autofonts}"; FontInstall: "JetBrains Mono Bold Italic"; Flags: onlyifdoesntexist uninsneveruninstall

; WebView2 Runtime bootstrapper - for installing WebView2 if not present
Source: "..\tools\webview2\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[Code]
// Get uninstall string from registry
function GetUninstallString(): String;
var
  UninstallKey: String;
  UninstallString: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8A7B3E5C-4D2F-4A1B-9C8E-5F6D7E8A9B0C}_is1';
  
  // Try 64-bit registry first
  if RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', UninstallString) then
    Result := UninstallString
  else if RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallString) then
    Result := UninstallString;
end;

// Check if previous version is installed
function IsOldVersionInstalled(): Boolean;
begin
  Result := GetUninstallString() <> '';
end;

// Get installation location from registry
function GetInstallLocation(): String;
var
  UninstallKey: String;
  InstallLocation: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8A7B3E5C-4D2F-4A1B-9C8E-5F6D7E8A9B0C}_is1';
  
  // Try to get InstallLocation from registry
  if RegQueryStringValue(HKLM, UninstallKey, 'InstallLocation', InstallLocation) then
    Result := InstallLocation
  else if RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', InstallLocation) then
    Result := InstallLocation;
end;

// Delete directory recursively
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

// Uninstall the old version silently and delete installation folder
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
    // Remove quotes if present
    if (Length(UninstallString) > 0) and (UninstallString[1] = '"') then
      UninstallString := RemoveQuotes(UninstallString);
    
    // Run uninstaller silently
    if not Exec(UninstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := False;
    end;
    
    // Delete remaining installation folder (in case uninstaller left files)
    if Result and (InstallLocation <> '') then
    begin
      // Wait a moment for uninstaller to finish file operations
      Sleep(1000);
      
      // Delete the installation directory
      if DirExists(InstallLocation) then
      begin
        DeleteDirectory(InstallLocation);
      end;
    end;
  end;
end;

// Check if .NET 8 Desktop Runtime is installed
function IsDotNet8DesktopInstalled: Boolean;
var
  Output: AnsiString;
  ExitCode: Integer;
  TempFile: String;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_check.txt');
  
  // Run dotnet --list-runtimes and save output to temp file
  if Exec('cmd.exe', '/c dotnet --list-runtimes > "' + TempFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    if ExitCode = 0 then
    begin
      if LoadStringFromFile(TempFile, Output) then
      begin
        // Check for Microsoft.WindowsDesktop.App 8.x
        if Pos('Microsoft.WindowsDesktop.App 8.', String(Output)) > 0 then
          Result := True;
      end;
    end;
  end;
  
  // Cleanup temp file
  DeleteFile(TempFile);
end;

// Check if WebView2 Runtime is installed (via registry)
function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  Result := False;
  
  // Check per-machine installation (64-bit)
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
  begin
    if Version <> '' then
      Result := True;
  end
  // Check per-machine installation (32-bit registry view)
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

// Install WebView2 Runtime if not present
function InstallWebView2: Boolean;
var
  ResultCode: Integer;
  ErrorCode: Integer;
begin
  Result := False;
  
  // Try to run the bundled bootstrapper silently
  if Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      Result := True;
    end
    else
    begin
      // Bootstrapper failed, offer manual download
      if MsgBox('WebView2 Runtime installation failed (Error: ' + IntToStr(ResultCode) + ').' + #13#10 + #13#10 +
                'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        ShellExec('open', '{#WebView2DownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      end;
    end;
  end
  else
  begin
    // Exec failed, offer manual download
    if MsgBox('Could not run WebView2 installer.' + #13#10 + #13#10 +
              'Would you like to download it manually?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', '{#WebView2DownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;

// Called before installation begins
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // Check for .NET 8 Desktop Runtime
  if not IsDotNet8DesktopInstalled() then
  begin
    if MsgBox('{#MyAppName} requires .NET 8 Desktop Runtime to run.' + #13#10 + #13#10 +
              'Would you like to download it now?' + #13#10 + #13#10 +
              'Click Yes to open the download page, then run this installer again after installing .NET 8.',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', '{#DotNetDownloadUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    
    MsgBox('Please install .NET 8 Desktop Runtime and run this installer again.', mbInformation, MB_OK);
    Result := False;
    Exit;
  end;
  
  // Check for WebView2 Runtime
  if not IsWebView2Installed() then
  begin
    if MsgBox('{#MyAppName} requires Microsoft Edge WebView2 Runtime.' + #13#10 + #13#10 +
              'WebView2 will be installed automatically. Continue?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      if not InstallWebView2() then
      begin
        if MsgBox('WebView2 installation may have failed. Continue with setup anyway?' + #13#10 + #13#10 +
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
  
  // Check for and uninstall previous version
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
