$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDir = Join-Path $root "packages\Microsoft.Web.WebView2"
$libDir = Join-Path $root "lib"
$version = "1.0.3351.48"
$packageUrl = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$version"
$nupkg = Join-Path $root "packages\Microsoft.Web.WebView2.$version.nupkg"

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $nupkg) | Out-Null

if (-not (Test-Path $nupkg)) {
  Invoke-WebRequest -Uri $packageUrl -OutFile $nupkg
}

if (Test-Path $packageDir) {
  Remove-Item -LiteralPath $packageDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $packageDir)

$dll = Get-ChildItem -Path $packageDir -Recurse -Filter "Microsoft.Web.WebView2.WinForms.dll" | Select-Object -First 1
if (-not $dll) {
  throw "WebView2 WinForms DLL was not found in the downloaded package."
}

New-Item -ItemType Directory -Force -Path $libDir | Out-Null
Copy-Item -LiteralPath (Get-ChildItem -Path $packageDir -Recurse -Filter "Microsoft.Web.WebView2.WinForms.dll" | Where-Object { $_.FullName -match "\\lib\\net462\\" } | Select-Object -First 1).FullName -Destination (Join-Path $libDir "Microsoft.Web.WebView2.WinForms.dll") -Force
Copy-Item -LiteralPath (Get-ChildItem -Path $packageDir -Recurse -Filter "Microsoft.Web.WebView2.Core.dll" | Where-Object { $_.FullName -match "\\lib\\net462\\" } | Select-Object -First 1).FullName -Destination (Join-Path $libDir "Microsoft.Web.WebView2.Core.dll") -Force
Copy-Item -LiteralPath (Get-ChildItem -Path $packageDir -Recurse -Filter "WebView2Loader.dll" | Where-Object { $_.FullName -match "\\runtimes\\win-x64\\native\\" } | Select-Object -First 1).FullName -Destination (Join-Path $libDir "WebView2Loader.dll") -Force

Write-Host "WebView2 dependency installed:"
Write-Host $libDir
