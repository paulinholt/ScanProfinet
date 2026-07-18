; ============================================================
;  ScanProfinet — script Inno Setup
;  Copyright (c) 2026 Paulo Leal Taveira
;  Compilar:  ISCC.exe ScanProfinet.iss
;  Recomendado: usar build-installer.ps1 (faz publish + ISCC)
; ============================================================

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\ScanProfinet\bin\Release\net8.0-windows\win-x64\publish"
#endif

; Caminho do instalador do Npcap. Coloque o npcap-x.xx.exe em installer\dependencies
; e passe /DNpcapInstaller="dependencies\npcap-1.79.exe" ao ISCC (o build-installer.ps1 detecta sozinho).
#ifndef NpcapInstaller
  #define NpcapInstaller ""
#endif

#define AppName      "ScanProfinet"
#define AppPublisher "Paulo Leal Taveira"
#define AppExeName   "ScanProfinet.exe"
#define AppIcon      "..\ScanProfinet\Resources\scanprofinet.ico"

#if !FileExists(AddBackslash(PublishDir) + AppExeName)
  #error Build nao encontrada em PublishDir. Rode build-installer.ps1 antes.
#endif

[Setup]
; AppId fixo — mantenha entre versoes para atualizar no lugar
AppId={{7C3A9E21-5F84-4B2D-9A6C-1E0F3B7D28A4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (c) 2026 Paulo Leal Taveira
DefaultDirName={autopf}\ScanProfinet
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=output
OutputBaseFilename=ScanProfinet-Setup-{#AppVersion}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english";             MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
#if NpcapInstaller != ""
Name: "npcap"; Description: "Instalar Npcap (driver necessario para o scan PROFINET/DCP)"; GroupDescription: "Componentes:"; Check: not NpcapInstalled
#endif

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#if NpcapInstaller != ""
Source: "{#NpcapInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion
#endif

[Icons]
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";       Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
#if NpcapInstaller != ""
Filename: "{tmp}\{#ExtractFileName(NpcapInstaller)}"; StatusMsg: "Instalando Npcap..."; Tasks: npcap; Flags: waituntilterminated
#endif
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function NpcapInstalled: Boolean;
begin
  Result := DirExists(ExpandConstant('{sys}\Npcap')) or
            RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Npcap') or
            RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Npcap');
end;
