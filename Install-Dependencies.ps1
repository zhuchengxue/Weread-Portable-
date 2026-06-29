$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = "1.0.3351.48"
$packagesRoot = Join-Path $root "packages"
$packageDir = Join-Path $packagesRoot "Microsoft.Web.WebView2"
$libDir = Join-Path $root "lib"
$packageUrl = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$version"
$nupkg = Join-Path $packagesRoot "Microsoft.Web.WebView2.$version.nupkg"
$download = $nupkg + ".download"

New-Item -ItemType Directory -Force -Path $packagesRoot | Out-Null

if (-not (Test-Path -LiteralPath $nupkg -PathType Leaf)) {
  try {
    Invoke-WebRequest -Uri $packageUrl -OutFile $download -UseBasicParsing
    Move-Item -LiteralPath $download -Destination $nupkg -Force
  }
  finally {
    if (Test-Path -LiteralPath $download) {
      Remove-Item -LiteralPath $download -Force
    }
  }
}

if (Test-Path -LiteralPath $packageDir) {
  Remove-Item -LiteralPath $packageDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $packageDir)

$sources = @{
  "Microsoft.Web.WebView2.WinForms.dll" = Join-Path $packageDir "lib\net462\Microsoft.Web.WebView2.WinForms.dll"
  "Microsoft.Web.WebView2.Core.dll" = Join-Path $packageDir "lib\net462\Microsoft.Web.WebView2.Core.dll"
  "WebView2Loader.dll" = Join-Path $packageDir "runtimes\win-x64\native\WebView2Loader.dll"
}

foreach ($source in $sources.Values) {
  if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Expected WebView2 dependency was not found: $source"
  }
}

New-Item -ItemType Directory -Force -Path $libDir | Out-Null
foreach ($entry in $sources.GetEnumerator()) {
  Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $libDir $entry.Key) -Force
}

Write-Host "Installed WebView2 $version dependencies: $libDir"
