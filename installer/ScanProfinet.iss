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

; Visual C++ 2015-2022 redistribuivel (x64) — necessario para o .NET 8 rodar em
; Windows Server 2012/2012 R2 (instala UCRT + runtime VC++). O build-installer.ps1
; detecta o vc_redist.x64.exe em installer\dependencies automaticamente.
#ifndef VcRedist
  #define VcRedist ""
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
; 6.2 = Windows 8 / Windows Server 2012. Permite instalar em SOs antigos suportados pelo .NET 8.
MinVersion=6.2
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
#if VcRedist != ""
Source: "{#VcRedist}"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion
#endif

[Icons]
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";       Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
#if VcRedist != ""
; /install /quiet e idempotente: se ja houver versao igual/mais nova, sai sem fazer nada.
Filename: "{tmp}\{#ExtractFileName(VcRedist)}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Instalando runtime do sistema (Visual C++ 2015-2022)..."; Flags: waituntilterminated
#endif
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
