<#
    build.ps1 - builds MetaHorizonLinkDoctor.exe

    No .NET SDK required: uses the C# compiler (csc.exe) that ships with
    Visual Studio / Build Tools 2017+ (".NET desktop development" workload)
    or VS Community.

    Output: dist\MetaHorizonLinkDoctor.exe (self-contained, runs on any
    Windows 10/11 with .NET Framework 4.8, which is part of the OS).

    Usage:
        powershell -ExecutionPolicy Bypass -File build.ps1
        powershell -ExecutionPolicy Bypass -File build.ps1 -Version 1.2.3 -OutputDir .\dist
#>
param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputDir) { $OutputDir = Join-Path $root "dist" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ---------------------------------------------------------------- locate csc.exe
function Find-Csc {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
                            -find "MSBuild\**\Bin\Roslyn\csc.exe" 2>$null |
                 Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }

    foreach ($base in @("$env:ProgramFiles\Microsoft Visual Studio", "${env:ProgramFiles(x86)}\Microsoft Visual Studio")) {
        if (-not (Test-Path $base)) { continue }
        $hit = Get-ChildItem $base -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
               Where-Object { $_.FullName -match "Roslyn" } |
               Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }

    $fw = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
    if (Test-Path (Join-Path $fw "csc.exe")) { return (Join-Path $fw "csc.exe") }

    throw "csc.exe not found. Install the Visual Studio Build Tools with '.NET desktop development'."
}

$csc = Find-Csc
Write-Host "csc: $csc"

# ---------------------------------------------------------------- .NET Framework references
$fwDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Management.dll",
    "System.ServiceProcess.dll"
) | ForEach-Object { Join-Path $fwDir $_ }

foreach ($r in $refs) {
    if (-not (Test-Path $r)) { throw "Missing reference: $r" }
}

$sources = Get-ChildItem (Join-Path $root "src") -Filter *.cs -Recurse |
           Sort-Object FullName | ForEach-Object { $_.FullName }

$iconPath = Join-Path $root "assets\icon.ico"
$outExe = Join-Path $OutputDir "MetaHorizonLinkDoctor.exe"

$cscArgs = New-Object System.Collections.Generic.List[string]
$cscArgs.Add("/nologo")
$cscArgs.Add("/target:winexe")
$cscArgs.Add("/platform:anycpu")
$cscArgs.Add("/optimize+")
$cscArgs.Add("/codepage:65001")
$cscArgs.Add("/warn:4")
$cscArgs.Add("/win32manifest:" + (Join-Path $root "src\app.manifest"))
$cscArgs.Add("/out:" + $outExe)

if (Test-Path $iconPath) { $cscArgs.Add("/win32icon:" + $iconPath) }

foreach ($r in $refs) { $cscArgs.Add("/reference:" + $r) }
foreach ($s in $sources) { $cscArgs.Add($s) }

# ---------------------------------------------------------------- generated AssemblyInfo
$fileVersion = if ($Version.Split(".").Count -ge 4) { $Version } else { "$Version.0" }

$objDir = Join-Path $root "obj"
New-Item -ItemType Directory -Force -Path $objDir | Out-Null
$assemblyInfo = Join-Path $objDir "AssemblyInfo.g.cs"

@"
using System.Reflection;
[assembly: AssemblyTitle("Meta Horizon Link Doctor")]
[assembly: AssemblyDescription("Diagnoses and fixes the Meta Horizon Link install failure on Windows 10/11")]
[assembly: AssemblyCompany("Meta Horizon Link Doctor contributors")]
[assembly: AssemblyProduct("Meta Horizon Link Doctor")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyVersion("$fileVersion")]
[assembly: AssemblyFileVersion("$fileVersion")]
[assembly: AssemblyInformationalVersion("$Version")]
"@ | Set-Content -Path $assemblyInfo -Encoding UTF8

$cscArgs.Add($assemblyInfo)

Write-Host "Compiling $($sources.Count) files..."
& $csc $cscArgs.ToArray()
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

$info = Get-Item $outExe
$hash = (Get-FileHash $outExe -Algorithm SHA256).Hash
Write-Host ""
Write-Host "OK  ->  $outExe"
Write-Host ("     {0:N0} bytes  |  SHA256 {1}" -f $info.Length, $hash)
