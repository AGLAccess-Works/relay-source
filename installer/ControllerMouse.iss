#ifndef AppVersion
  #define AppVersion "1.4.0-beta.20"
#endif
#ifndef AppFileVersion
  #define AppFileVersion "1.4.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\" + AppVersion + "\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{20880620-41DE-486D-AC32-F8B581A9A482}
AppName=Relay
AppVersion={#AppVersion}
AppPublisher=AGL Access Works
DefaultDirName={localappdata}\Programs\XboxControllerMouse
DefaultGroupName=Relay
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Relay-{#AppVersion}-win-x64-Setup
SetupIconFile=..\src\ControllerMouse.App\Assets\app.ico
UninstallDisplayIcon={app}\XboxControllerMouse.exe
UninstallDisplayName=Relay
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
CloseApplications=yes
RestartApplications=no
AppMutex=Local\XboxControllerMouse.App
SetupLogging=yes
VersionInfoVersion={#AppFileVersion}
VersionInfoDescription=Relay installer

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Relay"; Filename: "{app}\XboxControllerMouse.exe"
Name: "{autodesktop}\Relay"; Filename: "{app}\XboxControllerMouse.exe"; Tasks: desktopicon

[Registry]
; The app alone opts into startup. Uninstall removes only its own Run value.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "XboxControllerMouse"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\XboxControllerMouse.exe"; Description: "Open Relay"; Flags: nowait postinstall skipifsilent

; User settings are deliberately preserved across upgrades and uninstall/reinstall.
