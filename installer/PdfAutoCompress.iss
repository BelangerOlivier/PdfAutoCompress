; Inno Setup script for PDF Auto-Compress (per-user, no admin).
;
; Build (version injected by CI):
;   ISCC /DMyAppVersion=1.2.3 installer\PdfAutoCompress.iss
;
; Expects the published tray output folder at ..\dist\tray\ (self-contained, not single-file;
; see .github\workflows\release.yml). Produces ..\dist\installer\PdfAutoCompressSetup.exe.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.3"
#endif

#define MyAppName "PDF Auto-Compress"
#define MyAppExeName "PdfAutoCompress.exe"
#define MyAppPublisher "Olivier Belanger"
#define MyAppUrl "https://github.com/BelangerOlivier/PdfAutoCompress"

[Setup]
; AppId uniquely identifies this app across versions — never change it.
AppId={{D0573C90-B565-4B63-B046-32479F18C797}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}

; Per-user install, no elevation — matches the app's asInvoker manifest and %LOCALAPPDATA%/HKCU model.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\PdfAutoCompress
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Close the running app before upgrading and restart it afterwards — this is what removes the
; old "kill the process first" pain. The mutex name matches Program.cs's single-instance mutex.
AppMutex=PdfAutoCompress.SingleInstance
CloseApplications=force
RestartApplications=no

SetupIconFile=..\src\PdfAutoCompress.Tray\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
OutputDir=..\dist\installer
OutputBaseFilename=PdfAutoCompressSetup
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: "startup"; Description: "Launch automatically when I sign in to Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\dist\tray\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; Autostart via the same HKCU Run value StartupManager uses, so the two stay consistent.
; Removed on uninstall (uninsdeletevalue); only written when the Startup task is selected.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "PdfAutoCompress"; ValueData: """{app}\{#MyAppExeName}"" --startup"; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
; Launch the app right after an interactive install (skipped for /SILENT release-time builds).
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

; Relaunch after a SILENT in-app auto-update. The running app invokes the installer with
; "/SILENT /RELAUNCH=1"; the postinstall entry above is skipped when silent, so this one
; (gated on the flag) brings the app back. Normal /SILENT release builds don't pass it.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: WantRelaunch

; NOTE: user settings in %APPDATA%\PdfAutoCompress are intentionally left in place on uninstall.

[Code]
function WantRelaunch(): Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;
