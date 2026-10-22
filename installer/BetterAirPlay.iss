; Inno Setup script for the Better AirPlay installer.
; Built by .github/workflows/build.yml:
;   iscc /DAppVersion=1.2.3 /DAppDir=<staged app folder> /DOutDir=<dir> installer\BetterAirPlay.iss
; <staged app folder> holds BetterAirPlay.exe, its config, the notices, and receiver\ from scripts/bundle-receiver.sh.

#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z
#endif
#ifndef AppDir
  #error Pass /DAppDir=<staged app folder>
#endif
#ifndef OutDir
  #define OutDir "."
#endif

#define AppName "Better AirPlay"
#define AppExe "BetterAirPlay.exe"

[Setup]
AppId={{6F2B8E4D-3C1A-4B7E-9D5F-2A8C1E0B7F43}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Admin rights are needed for Program Files and the firewall rule below.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutDir}
OutputBaseFilename=BetterAirPlay-Setup-{#AppVersion}
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no

[Tasks]
Name: autostart; Description: "Start {#AppName} when I sign in"

[Files]
Source: "{#AppDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Same value the app's "Start with Windows" menu item writes, so either can turn it off.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; Without an inbound rule, Apple devices never discover the receiver. Private networks only.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""{#AppName} receiver"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""{#AppName} receiver"" dir=in action=allow profile=private program=""{app}\receiver\bin\uxplay.exe"" enable=yes"; Flags: runhidden
; Also runs on silent installs: the in-app updater (Updater.cs) installs with /SILENT and relies on this to restart the app.
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: postinstall nowait runasoriginaluser

[UninstallRun]
; Only stop processes running from this install, not some other UxPlay.
Filename: "powershell.exe"; Parameters: "-NoProfile -Command ""Get-Process BetterAirPlay, uxplay -ErrorAction SilentlyContinue | Where-Object Path -like '{app}\*' | Stop-Process -Force"""; Flags: runhidden; RunOnceId: "StopApp"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""{#AppName} receiver"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\{#AppName}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // The app may have written these itself, outside the installer's [Registry] section.
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\{#AppName}');
  end;
end;
