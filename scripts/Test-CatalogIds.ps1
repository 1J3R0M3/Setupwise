<#
.SYNOPSIS
    Checks that every package id in catalog/catalog.json exists in the official
    winget repository (github.com/microsoft/winget-pkgs).

.DESCRIPTION
    Runs on any OS with PowerShell 7; winget itself is not required.
    Set GITHUB_TOKEN to avoid the low rate limit for anonymous API requests.
#>
[CmdletBinding()]
param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..' 'catalog' 'catalog.json')
)

$ErrorActionPreference = 'Stop'

$json = Get-Content -LiteralPath $CatalogPath -Raw
$catalog = $json | ConvertFrom-Json   # PowerShell 7 accepts // comments
$headers = @{ 'User-Agent' = 'setupwise-catalog-check'; 'Accept' = 'application/vnd.github+json' }
if ($env:GITHUB_TOKEN) { $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN" }

function Test-WingetId([string]$Id) {
    # Manifests live in manifests/<first letter>/<Id with dots as folders>/<version>/
    $segments = $Id.Split('.') | ForEach-Object { [Uri]::EscapeDataString($_) }
    $path = "manifests/$($Id.Substring(0, 1).ToLowerInvariant())/$($segments -join '/')"
    try {
        $entries = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/microsoft/winget-pkgs/contents/$path"
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 404) { return $false }
        throw
    }
    # A package folder contains version folders, which start with a digit.
    # (A folder like "Mozilla/Firefox" also contains sub-packages such as "de".)
    return [bool]($entries | Where-Object { $_.type -eq 'dir' -and $_.name -match '^\d' })
}

$missing = @()
foreach ($app in $catalog.apps) {
    if (Test-WingetId $app.id) {
        Write-Host "  ok       $($app.id)"
    }
    else {
        Write-Host "  MISSING  $($app.id)" -ForegroundColor Red
        $missing += $app.id
    }
}

if ($missing.Count -gt 0) {
    Write-Error "$($missing.Count) package id(s) not found in winget-pkgs: $($missing -join ', ')"
}
Write-Host "All $(@($catalog.apps).Count) package ids exist."
