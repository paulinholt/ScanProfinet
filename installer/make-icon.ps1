# Gera o ícone do ScanProfinet (256x256 PNG embutido em .ico).
# Uso:  powershell -ExecutionPolicy Bypass -File make-icon.ps1
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Fundo arredondado (azul -> navy)
$rect = New-Object System.Drawing.Rectangle(8, 8, ($size-16), ($size-16))
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 48
$path.AddArc($rect.X, $rect.Y, $r, $r, 180, 90)
$path.AddArc($rect.Right-$r, $rect.Y, $r, $r, 270, 90)
$path.AddArc($rect.Right-$r, $rect.Bottom-$r, $r, $r, 0, 90)
$path.AddArc($rect.X, $rect.Bottom-$r, $r, $r, 90, 90)
$path.CloseFigure()

$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(37, 99, 235),
    [System.Drawing.Color]::FromArgb(15, 27, 45),
    45.0)
$g.FillPath($brush, $path)

# Nós de rede (topologia estrela) em branco
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$penW  = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 255, 255, 255), 7)

$cx = 128; $cy = 128
$nodes = @(
    @(128, 60), @(196, 104), @(196, 176),
    @(128, 208), @(60, 176), @(60, 104)
)
# linhas do centro para cada nó
foreach ($n in $nodes) { $g.DrawLine($penW, $cx, $cy, $n[0], $n[1]) }
# nós externos
foreach ($n in $nodes) { $g.FillEllipse($white, $n[0]-16, $n[1]-16, 32, 32) }
# nó central (destaque)
$accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(96, 165, 250))
$g.FillEllipse($white, $cx-26, $cy-26, 52, 52)
$g.FillEllipse($accent, $cx-16, $cy-16, 32, 32)

$g.Dispose()

# Salva PNG temporário
$pngPath = Join-Path $PSScriptRoot "icon-temp.png"
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# Monta o arquivo .ico (1 entrada, PNG embutido — suportado no Windows Vista+)
$png = [System.IO.File]::ReadAllBytes($pngPath)
$icoPath = Join-Path $PSScriptRoot "..\ScanProfinet\Resources\scanprofinet.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type = icon
$bw.Write([UInt16]1)      # count
# ICONDIRENTRY
$bw.Write([Byte]0)        # width  (0 = 256)
$bw.Write([Byte]0)        # height (0 = 256)
$bw.Write([Byte]0)        # colors
$bw.Write([Byte]0)        # reserved
$bw.Write([UInt16]1)      # planes
$bw.Write([UInt16]32)     # bpp
$bw.Write([UInt32]$png.Length)  # size
$bw.Write([UInt32]22)     # offset (6 + 16)
$bw.Write($png)
$bw.Flush(); $bw.Close(); $fs.Close()
Remove-Item $pngPath -ErrorAction SilentlyContinue

Write-Host "Ícone gerado em $icoPath"
