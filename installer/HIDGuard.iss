#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#ifndef MyAppSourceDir
#define MyAppSourceDir "..\dist\app"
#endif

#ifndef MySimulatorSourceDir
#define MySimulatorSourceDir "..\dist\simulator"
#endif

#ifndef MyOutputDir
#define MyOutputDir "..\artifacts"
#endif

#define MyAppName "HIDGuard"
#define MyAppPublisher "Boran"
#define MyAppURL "https://github.com/Bor-Code/HIDGuard"
#define MyAppExeName "HidGuard.App.exe"
#define MySimulatorExeName "HidGuard.Simulator.exe"

[Setup]
AppId={{C6D29A2F-1738-4F70-985B-B77A0E18544D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#MyOutputDir}
OutputBaseFilename=HIDGuard-Setup-v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start HIDGuard automatically on Windows logon"; GroupDescription: "Startup options:"
Name: "simulator"; Description: "Install HIDGuard Attack Simulator CLI tools"; GroupDescription: "Optional components:"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MySimulatorSourceDir}\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: simulator

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "HIDGuard"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
