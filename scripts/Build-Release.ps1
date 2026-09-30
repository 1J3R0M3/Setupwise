<#
.SYNOPSIS
    Builds the release files into artifacts/dist:
      Setupwise-<version>-Setup-x64.exe     (Windows only, needs Inno Setup)
      Setupwise-<version>-portable-x64.zip
      SHA256SUMS.txt

.DESCRIPTION
    The build runs in stages so that code signing can happen in between (see release.yml):
      Publish    dotnet publish into artifacts/publish
      Package    installer + portable zip from artifacts/publish into artifacts/dist
      Checksums  SHA256SUMS.txt for everything in artifacts/dist
    Without -Stage, all stages run.

.EXAMPLE
    ./scripts/Build-Release.ps1                    # version from Directory.Build.props
    ./scripts/Build-Release.ps1 -Version 0.2.0
    ./scripts/Build-Release.ps1 -SkipInstaller     # e.g. on Linux: only the portable zip
    ./scripts/Build-Release.ps1 -Stage Publish
#>
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateSet('All', 'Publish', 'Package', 'Checksums')]
    [string]$Stage = 'All',
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

function Invoke-Publish {
    Write-Host "Publishing Setupwise $Version"
    Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish "-p:Version=$Version" -nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}

function Find-Iscc {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')) | Where-Object { $_ }
    Get-ChildItem -Path ($roots | ForEach-Object { Join-Path $_ 'Inno Setup *\ISCC.exe' }) -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}

function Invoke-Package {
    if (-not (Test-Path (Join-Path $publish 'Setupwise.exe'))) { throw "Nothing published in $publish. Run the Publish stage first." }
    Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $dist | Out-Null

    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath (Join-Path $dist "Setupwise-$Version-portable-x64.zip")

    if ($SkipInstaller) { return }
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

function Invoke-Checksums {
    $sums = Get-ChildItem $dist -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    }
    Set-Content -Path (Join-Path $dist 'SHA256SUMS.txt') -Value $sums -Encoding ascii

    Write-Host "`nRelease files:"
    Get-ChildItem $dist | ForEach-Object { Write-Host ('  {0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB)) }
}

if ($Stage -in 'All', 'Publish') { Invoke-Publish }
if ($Stage -in 'All', 'Package') { Invoke-Package }
if ($Stage -in 'All', 'Package', 'Checksums') { Invoke-Checksums }
