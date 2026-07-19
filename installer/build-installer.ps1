# ============================================================
#  Build do instalador ScanProfinet
#  - Publica a aplicacao self-contained (nao exige .NET instalado)
#  - Gera o icone
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
$root       = Split-Path -Parent $PSScriptRoot
$proj       = Join-Path $root "ScanProfinet\ScanProfinet.csproj"
$publishDir = Join-Path $root "ScanProfinet\bin\Release\net8.0-windows\win-x64\publish"
$iss        = Join-Path $PSScriptRoot "ScanProfinet.iss"

Write-Host "== 1/4  Gerando icone ==" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "make-icon.ps1")

Write-Host "== 2/4  Publicando aplicacao (self-contained win-x64) ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true /p:Version=$Version /p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

$depDir = Join-Path $PSScriptRoot "dependencies"

# Detecta automaticamente o instalador do Npcap na pasta dependencies
$npcap = Get-ChildItem $depDir -Filter "npcap*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
$npcapArg = ""
if ($null -ne $npcap) {
    $npcapArg = "/DNpcapInstaller=dependencies\" + $npcap.Name
    Write-Host ("   Npcap encontrado: " + $npcap.Name + " - sera embutido.") -ForegroundColor Green
} else {
    Write-Warning "   Npcap NAO encontrado em dependencies. O instalador saira sem o driver."
    Write-Warning "   Baixe em https://npcap.com e coloque o .exe em installer/dependencies."
}

# Detecta o Visual C++ redistribuivel (necessario para .NET 8 em Windows Server 2012/2012 R2)
$vc = Get-ChildItem $depDir -Filter "vc_redist*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
$vcArg = ""
if ($null -ne $vc) {
    $vcArg = "/DVcRedist=dependencies\" + $vc.Name
    Write-Host ("   VC++ redist encontrado: " + $vc.Name + " - sera embutido.") -ForegroundColor Green
} else {
    Write-Warning "   vc_redist.x64.exe NAO encontrado. O app pode nao abrir em Windows Server 2012/2012 R2."
    Write-Warning "   Baixe em https://aka.ms/vs/17/release/vc_redist.x64.exe e coloque em installer/dependencies."
}

Write-Host "== 3/4  Localizando Inno Setup (ISCC) ==" -ForegroundColor Cyan
$iscc = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 nao encontrado. Instale de https://jrsoftware.org/isdl.php" }

Write-Host "== 4/4  Compilando instalador ==" -ForegroundColor Cyan
$isccArgs = @(("/DAppVersion=" + $Version), ("/DPublishDir=" + $publishDir))
if ($npcapArg -ne "") { $isccArgs += $npcapArg }
if ($vcArg -ne "")    { $isccArgs += $vcArg }
$isccArgs += $iss
& $iscc $isccArgs
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou." }

Write-Host ""
Write-Host ("Concluido. Instalador em: " + (Join-Path $PSScriptRoot "output")) -ForegroundColor Green
