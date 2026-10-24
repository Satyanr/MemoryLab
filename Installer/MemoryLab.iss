#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\bin\Release\net8.0-windows\win-x64\publish"
#endif
#ifndef InstallerOutput
  #define InstallerOutput SourcePath
#endif

[Setup]
#ifdef InstallerTest
AppId={{5823E672-6A43-49A9-91F0-DFBBEB146AD0}
#else
AppId={{6B5D54A1-839B-4DF8-BC4E-B6EB0230BC16}
#endif
AppName=MemoryLab
AppVersion={#AppVersion}
AppVerName=MemoryLab {#AppVersion}
AppPublisher=MemoryLab
VersionInfoDescription=MemoryLab Installer
#ifdef InstallerTest
DefaultDirName={#TestRoot}\App
#else
DefaultDirName={localappdata}\Programs\MemoryLab
#endif
DefaultGroupName=MemoryLab
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
WizardImageFile=..\Assets\Brand\InstallerSidebar.bmp
WizardSmallImageFile=..\Assets\Brand\InstallerHeader.bmp
SetupIconFile=..\Assets\Brand\MemoryLab.ico
UninstallDisplayIcon={app}\MemoryLab.exe
UninstallDisplayName=MemoryLab
OutputDir={#InstallerOutput}
OutputBaseFilename=MemoryLab-Setup-{#AppVersion}-x64
#ifdef InstallerTest
Compression=zip/1
SolidCompression=no
#else
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
#endif
DisableProgramGroupPage=yes
DisableWelcomePage=no
CloseApplications=yes
CloseApplicationsFilter=MemoryLab.exe
RestartApplications=no
SetupLogging=yes
#ifdef InstallerTest
CreateUninstallRegKey=no
UsePreviousAppDir=no
UsePreviousTasks=no
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
#ifdef InstallerTest
Name: "{#TestRoot}\Desktop\MemoryLab"; Filename: "{app}\MemoryLab.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MemoryLab.exe"; Tasks: desktopicon
Name: "{#TestRoot}\StartMenu\MemoryLab"; Filename: "{app}\MemoryLab.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MemoryLab.exe"
#else
Name: "{autodesktop}\MemoryLab"; Filename: "{app}\MemoryLab.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MemoryLab.exe"; Tasks: desktopicon
Name: "{autoprograms}\MemoryLab"; Filename: "{app}\MemoryLab.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MemoryLab.exe"
#endif

[Run]
Filename: "{app}\MemoryLab.exe"; Description: "Launch MemoryLab"; Flags: nowait postinstall skipifsilent
