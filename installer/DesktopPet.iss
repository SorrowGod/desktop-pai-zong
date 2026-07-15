#ifndef SourceDir
  #define SourceDir "..\artifacts\release\publish"
#endif
#ifndef OutputDir
  #define OutputDir "output"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "桌面猫咪"
#define AppExeName "DesktopPet.exe"
#define PublisherName "DesktopPet"

[Setup]
AppId={{6DC696BA-85E1-495C-86B9-2E5DD0248AD6}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#PublisherName}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\DesktopPet
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=DesktopPet-Setup-{#AppVersion}-win-x64
SetupIconFile=..\app\DesktopPet.App\Assets\DesktopPet.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DesktopPet');
  end;
end;
