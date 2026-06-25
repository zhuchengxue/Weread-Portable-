$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $root "WeReadReader.cs"
$output = Join-Path $root "WeReadReader.exe"
$icon = Join-Path $root "assets\weread.ico"
$lib = Join-Path $root "lib"
$coreDll = Join-Path $lib "Microsoft.Web.WebView2.Core.dll"
$winFormsDll = Join-Path $lib "Microsoft.Web.WebView2.WinForms.dll"

if (-not (Test-Path $csc)) {
  throw "C# compiler was not found: $csc"
}

& $csc /nologo /target:winexe /optimize+ /platform:x64 `
  /out:$output `
  /win32icon:$icon `
  /reference:System.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  "/reference:$coreDll" `
  "/reference:$winFormsDll" `
  $source

if ($LASTEXITCODE -ne 0) {
  throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host $output
