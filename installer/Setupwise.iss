; Setupwise installer (Inno Setup 6.3+ / 7)
;
; Built by CI (see .github/workflows). Locally:
;   dotnet publish src/Setupwise.App -c Release -r win-x64 --self-contained -o artifacts/publish
;   iscc /DAppVersion=0.1.0 installer/Setupwise.iss

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
; Numeric part for the Windows version resource (no "-beta" suffix allowed there)
#ifndef NumericVersion
  #define NumericVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif

#define AppName "Setupwise"
#define AppExe "Setupwise.exe"
#define RepoUrl "https://github.com/1J3R0M3/Setupwise"

[Setup]
; Never change the AppId: Windows uses it to find existing installations for updates.
AppId={{4F0DD8DE-94E2-40A2-ABAF-F700DEA2DCE7}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Setupwise contributors
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#NumericVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE

; Installs without admin rights for the current user; the user can choose "all users" instead.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

OutputDir=Output
OutputBaseFilename={#AppName}-{#AppVersion}-Setup-x64
SetupIconFile=..\assets\setupwise.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
english.AssociateFiles=Open Setupwise selections (*.setupwise) with {#AppName}
german.AssociateFiles=Setupwise-Auswahldateien (*.setupwise) mit {#AppName} öffnen
english.SelectionFileType=Setupwise selection
german.SelectionFileType=Setupwise-Auswahl
english.OtherTasks=Other:
german.OtherTasks=Sonstiges:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate"; Description: "{cm:AssociateFiles}"; GroupDescription: "{cm:OtherTasks}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; HKA = HKLM for "all users" installs, HKCU otherwise
Root: HKA; Subkey: "Software\Classes\.setupwise"; ValueType: string; ValueName: ""; ValueData: "Setupwise.Selection"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Setupwise.Selection"; ValueType: string; ValueName: ""; ValueData: "{cm:SelectionFileType}"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Setupwise.Selection\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Setupwise.Selection\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: associate

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
