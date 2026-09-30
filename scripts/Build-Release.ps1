<#
.SYNOPSIS
    Builds the release files into artifacts/dist:
      Setupwise-<version>-Setup-x64.exe     (Windows only, needs Inno Setup)
      Setupwise-<version>-portable-x64.zip
      SHA256SUMS.txt

.EXAMPLE
    ./scripts/Build-Release.ps1                    # version from Directory.Build.props
    ./scripts/Build-Release.ps1 -Version 0.2.0
    ./scripts/Build-Release.ps1 -SkipInstaller     # e.g. on Linux: only the portable zip
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src/Setupwise.App/Setupwise.App.csproj'
$publish = Join-Path $root 'artifacts/publish'
$dist = Join-Path $root 'artifacts/dist'

if (-not $Version) {
    $Version = (& dotnet msbuild $project -getProperty:Version).Trim()
}
$numeric = ($Version -split '[-+]')[0]
Write-Host "Building Setupwise $Version"

Remove-Item $publish, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $dist | Out-Null

& dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish "-p:Version=$Version" -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $dist "Setupwise-$Version-portable-x64.zip")

if (-not $SkipInstaller) {
    function Find-Iscc {
        $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
        $roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')) | Where-Object { $_ }
        Get-ChildItem -Path ($roots | ForEach-Object { Join-Path $_ 'Inno Setup *\ISCC.exe' }) -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }

    $iscc = Find-Iscc
    if (-not $iscc -and (Get-Command choco -ErrorAction SilentlyContinue)) {
        Write-Host 'Installing Inno Setup via Chocolatey...'
        & choco install innosetup --no-progress -y | Out-Null
        $iscc = Find-Iscc
    }
    if (-not $iscc) { throw 'Inno Setup (ISCC.exe) was not found. Install it from https://jrsoftware.org or use -SkipInstaller.' }

    & $iscc "/DAppVersion=$Version" "/DNumericVersion=$numeric" "/DSourceDir=$publish" "/O$dist" (Join-Path $root 'installer/Setupwise.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }
}

$sums = Get-ChildItem $dist -File | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
Set-Content -Path (Join-Path $dist 'SHA256SUMS.txt') -Value $sums -Encoding ascii

Write-Host "`nRelease files:"
Get-ChildItem $dist | ForEach-Object { Write-Host ('  {0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB)) }
