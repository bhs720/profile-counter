; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "ProFile Counter"
#define MyAppVersion "3.2.0"
#define MyAppPublisher "Ben Smith"
#define MyAppURL "https://bhs720.github.io/profile-counter"
#define MyAppExeName "ProFile Counter.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{C18240FC-FAD5-49CF-A67E-8C6912F6F142}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=.\releases\{#MyAppVersion}
OutputBaseFilename=ProFileCounterSetup{#MyAppVersion}
SetupIconFile=..\vs2015\app\tpcIcon.ico
Compression=lzma
SolidCompression=yes
UninstallDisplayIcon={app}\tpcIcon.ico
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\vs2015\x64\Release\ProFile Counter.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\vs2015\x64\Release\mupdf.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\vs2015\x64\Release\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files
Source: "..\vs2015\app\tpcIcon.ico"; DestDir: "{app}"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\icon"; Filename: "{app}\tpcIcon.ico"; IconFilename: "{app}\tpcIcon.ico"
Name: "{group}\{cm:UninstallProgram, {#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\tpcIcon.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"

[InstallDelete]
Type: files; Name: "{localappdata}/ProFile Counter/UserSettings.xml"

[UninstallDelete]
Type: files; Name: "{localappdata}/ProFile Counter/UserSettings.xml"
Type: filesandordirs; Name: "{localappdata}/ProFile Counter"

