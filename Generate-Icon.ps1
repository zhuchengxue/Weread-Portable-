$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$assets = Join-Path $root "assets"
$pngPath = Join-Path $assets "weread-official.png"
$roundedPngPath = Join-Path $assets "weread-rounded.png"
$icoPath = Join-Path $assets "weread.ico"

if (-not (Test-Path $pngPath)) {
  throw "Missing official icon PNG: $pngPath"
}

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Image]::FromFile($pngPath)
$size = 152
$radius = 34
$bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.Clear([System.Drawing.Color]::Transparent)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$diameter = $radius * 2
$path.AddArc(0, 0, $diameter, $diameter, 180, 90)
$path.AddArc($size - $diameter, 0, $diameter, $diameter, 270, 90)
$path.AddArc($size - $diameter, $size - $diameter, $diameter, $diameter, 0, 90)
$path.AddArc(0, $size - $diameter, $diameter, $diameter, 90, 90)
$path.CloseFigure()

$graphics.SetClip($path)
$graphics.DrawImage($source, 0, 0, $size, $size)
$graphics.ResetClip()

$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Save($roundedPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $stream.ToArray()
$stream.Dispose()
$path.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
$source.Dispose()

$file = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$writer = New-Object System.IO.BinaryWriter($file)

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$pngBytes.Length)
$writer.Write([UInt32]22)
$writer.Write($pngBytes)

$writer.Close()
$file.Close()
Write-Host $icoPath
