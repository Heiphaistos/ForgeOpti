; Compiler : iscc /DAppVersion=0.2.0 /DSourceDir=<dossier publish> /DMode=setup|portable installer\ForgeOpti.iss
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\out"
#endif
#ifndef Mode
  #define Mode "setup"
#endif

[Setup]
AppId={{6D4F0C1E-7B7A-4E0B-9F57-F0B0A7C1E0A1}
AppName=ForgeOpti
AppVersion={#AppVersion}
AppPublisher=Heiphaistos
OutputDir=..\dist
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=../assets/forgeopti.ico
UninstallDisplayIcon={app}\ForgeOpti.exe
#if Mode == "setup"
OutputBaseFilename=ForgeOpti-Setup-{#AppVersion}
DefaultDirName={autopf}\ForgeOpti
PrivilegesRequired=admin
DisableProgramGroupPage=yes
#else
; Portable : un seul .exe qui s'extrait dans le profil et lance l'appli, sans assistant ni entrée dans « Programmes ».
OutputBaseFilename=ForgeOpti-Portable-{#AppVersion}
DefaultDirName={localappdata}\ForgeOpti-Portable\{#AppVersion}
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
DisableWelcomePage=yes
#endif

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

#if Mode == "setup"
[Icons]
Name: "{autoprograms}\ForgeOpti"; Filename: "{app}\ForgeOpti.exe"
Name: "{autodesktop}\ForgeOpti"; Filename: "{app}\ForgeOpti.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; Flags: unchecked

[Run]
Filename: "{app}\ForgeOpti.exe"; Description: "Lancer ForgeOpti"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ForgeOpti-Verification /F"; Flags: runhidden; RunOnceId: "SupprTacheVerification"
#else
[Run]
Filename: "{app}\ForgeOpti.exe"; Flags: nowait
#endif
