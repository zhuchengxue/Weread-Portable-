param(
  [switch]$Package
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $root "WeReadReader.cs"
$output = Join-Path $root "WeReadReader.exe"
$config = Join-Path $root "WeReadReader.exe.config"
$readme = Join-Path $root "README.md"
$assets = Join-Path $root "assets"
$icon = Join-Path $assets "weread.ico"
$roundedIcon = Join-Path $assets "weread-rounded.png"
$lib = Join-Path $root "lib"
$coreDll = Join-Path $lib "Microsoft.Web.WebView2.Core.dll"
$winFormsDll = Join-Path $lib "Microsoft.Web.WebView2.WinForms.dll"
$loaderDll = Join-Path $lib "WebView2Loader.dll"
$archive = Join-Path $root "WeReadSideReader-Portable.zip"

$requiredFiles = @(
  $csc,
  $source,
  $config,
  $readme,
  $icon,
  $roundedIcon,
  $coreDll,
  $winFormsDll,
  $loaderDll
)

foreach ($file in $requiredFiles) {
  if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
    throw "Required build file was not found: $file"
  }
}

& $csc /nologo /target:winexe /optimize+ /platform:x64 /warn:4 /utf8output `
  /out:$output `
  /win32icon:$icon `
  /reference:System.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  "/reference:$coreDll" `
  "/reference:$winFormsDll" `
  $source

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output -PathType Leaf)) {
  throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built: $output"

if ($Package) {
  $stage = Join-Path ([System.IO.Path]::GetTempPath()) ("weread-portable-" + [Guid]::NewGuid().ToString("N"))
  try {
    New-Item -ItemType Directory -Path $stage | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $stage "assets") | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $stage "lib") | Out-Null

    Copy-Item -LiteralPath $output, $config, $readme -Destination $stage
    Copy-Item -LiteralPath $icon, $roundedIcon -Destination (Join-Path $stage "assets")
    Copy-Item -LiteralPath $coreDll, $winFormsDll, $loaderDll -Destination (Join-Path $stage "lib")

    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $archive -CompressionLevel Optimal -Force
  }
  finally {
    if (Test-Path -LiteralPath $stage) {
      Remove-Item -LiteralPath $stage -Recurse -Force
    }
  }

  Write-Host "Packaged: $archive"
}
