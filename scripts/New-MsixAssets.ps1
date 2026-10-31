<#
.SYNOPSIS
    Regenerates the MSIX logo files in installer/msix/Assets from assets/setupwise-512.png (Windows only).

.DESCRIPTION
    Only needed when the app icon changes; the generated PNG files are committed.
    File names follow the MSIX resource qualifiers (scale-*, targetsize-*, altform-unplated) that
    Build-Msix.ps1 indexes into resources.pri with MakePri.exe.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source = Join-Path $root 'assets/setupwise-512.png'
$target = Join-Path $root 'installer/msix/Assets'
New-Item -ItemType Directory -Path $target -Force | Out-Null

$sizes = [ordered]@{
    'StoreLogo.scale-100.png'                                   = 50
    'StoreLogo.scale-200.png'                                   = 100
    'Square44x44Logo.scale-100.png'                             = 44
    'Square44x44Logo.scale-200.png'                             = 88
    'Square150x150Logo.scale-100.png'                           = 150
    'Square150x150Logo.scale-200.png'                           = 300
}
foreach ($size in 16, 24, 32, 48, 256) {
    $sizes["Square44x44Logo.targetsize-$size.png"] = $size
    $sizes["Square44x44Logo.targetsize-${size}_altform-unplated.png"] = $size
}

$image = [System.Drawing.Image]::FromFile($source)
try {
    foreach ($entry in $sizes.GetEnumerator()) {
        $bitmap = New-Object System.Drawing.Bitmap($entry.Value, $entry.Value)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($image, 0, 0, $entry.Value, $entry.Value)
            $graphics.Dispose()
            $bitmap.Save((Join-Path $target $entry.Key), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
        Write-Host ('  {0,-55} {1,4} px' -f $entry.Key, $entry.Value)
    }
}
finally {
    $image.Dispose()
}
