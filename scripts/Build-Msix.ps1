<#
.SYNOPSIS
    Builds the Microsoft Store package (R-13) into artifacts/msix (Windows only, needs the Windows SDK):
      Setupwise-<version>-x64.msix        unsigned; the Store signs it after certification
      layout/                              the unpacked package, for a test with Add-AppxPackage -Register

.DESCRIPTION
    Publishes Setupwise with -p:SetupwiseFlavor=Store (no own update check, no "Run as administrator"),
    adds installer/msix/AppxManifest.xml and the logos, indexes the logos with MakePri.exe and packs everything
    with MakeAppx.exe. The package identity comes from installer/msix/identity.json.

.EXAMPLE
    ./scripts/Build-Msix.ps1                          # version from Directory.Build.props
    ./scripts/Build-Msix.ps1 -Version 1.0.0
    ./scripts/Build-Msix.ps1 -Version 0.3.0-ci.42 -Revision 42
#>
[CmdletBinding()]
param(
    [string]$Version,
    # The Store requires 0; a CI build may use the run number so that a test install can be updated in place.
    [ValidateRange(0, 65535)]
    [int]$Revision = 0
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src/Setupwise.App/Setupwise.App.csproj'
$template = Join-Path $root 'installer/msix'
$output = Join-Path $root 'artifacts/msix'
$layout = Join-Path $output 'layout'

if (-not $Version) {
    $Version = (& dotnet msbuild $project -getProperty:Version).Trim()
}
$numeric = [version](($Version -split '[-+]')[0])
$packageVersion = '{0}.{1}.{2}.{3}' -f $numeric.Major, $numeric.Minor, [Math]::Max($numeric.Build, 0), $Revision
if ($numeric.Major -eq 0 -or $Revision -ne 0) {
    Write-Warning "Package version $packageVersion can be tested, but not submitted to the Store (first number must not be 0, last must be 0)."
}

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $tool = Get-ChildItem -Path (Join-Path $kits "10.*/x64/$name") -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $tool) { throw "$name was not found. Install the Windows SDK (it contains MakeAppx.exe and MakePri.exe)." }
    return $tool
}

Write-Host "Publishing Setupwise $Version (Store flavour)"
Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
& dotnet publish $project -c Release -r win-x64 --self-contained true -o $layout "-p:Version=$Version" -p:SetupwiseFlavor=Store -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# ---- Manifest and logos -------------------------------------------------------------------------
$identity = Get-Content (Join-Path $template 'identity.json') -Raw | ConvertFrom-Json
$escape = { param($text) [System.Security.SecurityElement]::Escape($text) }
$manifest = (Get-Content (Join-Path $template 'AppxManifest.xml') -Raw).
    Replace('$IdentityName$', (& $escape $identity.name)).
    Replace('$Publisher$', (& $escape $identity.publisher)).
    Replace('$PublisherDisplayName$', (& $escape $identity.publisherDisplayName)).
    Replace('$Version$', $packageVersion)
if ($manifest -match '\$[A-Za-z]+\$') { throw "Unreplaced token $($Matches[0]) in AppxManifest.xml" }
[System.IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object System.Text.UTF8Encoding($false)))
Copy-Item (Join-Path $template 'Assets') (Join-Path $layout 'Assets') -Recurse

# resources.pri lets Windows pick the right logo size (scale-200, targetsize-24, ...). Only the logos are
# indexed: the published app has a "de" folder (WPF satellite assemblies) that MakePri would take for a language.
$makepri = Find-SdkTool 'makepri.exe'
$priRoot = Join-Path $output 'pri'
Copy-Item (Join-Path $template 'Assets') (Join-Path $priRoot 'Assets') -Recurse
& $makepri new /pr $priRoot /cf (Join-Path $template 'priconfig.xml') /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw 'MakePri new failed' }

# ---- Package ------------------------------------------------------------------------------------
$makeappx = Find-SdkTool 'makeappx.exe'
$package = Join-Path $output "Setupwise-$Version-x64.msix"
& $makeappx pack /d $layout /p $package /o | Where-Object { $_ -notmatch '^(Processing |Its path|\s*$)' }
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx pack failed' }

Write-Host "`nPackage $packageVersion ($($identity.name)):"
Write-Host ('  {0}  {1,8:N1} MB' -f (Split-Path $package -Leaf), ((Get-Item $package).Length / 1MB))
