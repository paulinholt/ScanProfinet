# ============================================================
#  Build do instalador ScanProfinet
#  - Publica a aplicação self-contained (não exige .NET instalado)
#  - Gera o ícone
#  - Compila o instalador com Inno Setup (ISCC)
#
#  Uso:
#     powershell -ExecutionPolicy Bypass -File build-installer.ps1
#     (opcional) -Version 1.0.1
# ============================================================
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root      = Split-Path -Parent $PSScriptRoot
$proj      = Join-Path $root "ScanProfinet\ScanProfinet.csproj"
$publishDir = Join-Path $root "ScanProfinet\bin\Release\net8.0-windows\win-x64\publish"
$iss       = Join-Path $PSScriptRoot "ScanProfinet.iss"

Write-Host "== 1/4  Gerando ícone ==" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "make-icon.ps1")

Write-Host "== 2/4  Publicando aplicação (self-contained win-x64) ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    /p:Version=$Version /p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

# Detecta automaticamente o instalador do Npcap na pasta dependencies
$npcap = Get-ChildItem (Join-Path $PSScriptRoot "dependencies") -Filter "npcap*.exe" -ErrorAction SilentlyContinue |
         Select-Object -First 1
$npcapArg = ""
if ($npcap) {
    $npcapArg = "/DNpcapInstaller=dependencies\$($npcap.Name)"
    Write-Host "   Npcap encontrado: $($npcap.Name) — será embutido." -ForegroundColor Green
} else {
    Write-Warning "   Npcap NÃO encontrado em dependencies\. O instalador sairá sem o driver."
    Write-Warning "   Baixe em https://npcap.com e coloque o .exe em installer\dependencies\."
}

Write-Host "== 3/4  Localizando Inno Setup (ISCC) ==" -ForegroundColor Cyan
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 não encontrado. Instale de https://jrsoftware.org/isdl.php" }

Write-Host "== 4/4  Compilando instalador ==" -ForegroundColor Cyan
& $iscc "/DAppVersion=$Version" "/DPublishDir=$publishDir" $npcapArg $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou." }

Write-Host "`nConcluído. Instalador em: $(Join-Path $PSScriptRoot 'output')" -ForegroundColor Green
