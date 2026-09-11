<#
.SYNOPSIS
    Downloads the two upstream tools this project depends on:
    ByeDPI (ciadpi.exe) and ProxiFyre.

.DESCRIPTION
    Both tools are third-party open-source projects and are NOT redistributed
    with this repository. This helper fetches the official x64 releases and
    places them where Install-RobloxServices.ps1 expects them.

      ByeDPI   -> %USERPROFILE%\.local\bin\ciadpi.exe
      ProxiFyre-> %LOCALAPPDATA%\Programs\ProxiFyre\

    Existing files are kept unless -Force is specified.
    Running as Administrator is recommended so ProxiFyre can install its driver.

.PARAMETER Force
    Re-download and overwrite files that already exist.

.EXAMPLE
    .\Get-Dependencies.ps1
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # much faster Invoke-WebRequest

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }

$byeDpiDir = Join-Path $env:USERPROFILE '.local\bin'
$byeDpiExe = Join-Path $byeDpiDir 'ciadpi.exe'
$proxiFyreDir = Join-Path $env:LOCALAPPDATA 'Programs\ProxiFyre'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("byedpi-deps-" + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    # ------------------------------------------------------------------ ByeDPI
    if ((Test-Path -LiteralPath $byeDpiExe) -and -not $Force) {
        Write-Host "ByeDPI already present: $byeDpiExe (use -Force to re-download)" -ForegroundColor Yellow
    }
    else {
        Write-Step 'Resolving the latest ByeDPI release'
        $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/hufrea/byedpi/releases/latest' `
            -Headers @{ 'User-Agent' = 'byedpi-roblox-control' }

        $asset = $release.assets | Where-Object { $_.name -like '*x86_64-w64.zip' } | Select-Object -First 1
        if (-not $asset) { throw 'Could not find an x86_64-w64 asset in the latest ByeDPI release.' }

        Write-Host "    Version: $($release.tag_name)"
        Write-Host "    Asset  : $($asset.name)"

        $zipPath = Join-Path $tempRoot $asset.name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath

        $extractDir = Join-Path $tempRoot 'byedpi'
        Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

        $binary = Get-ChildItem -Path $extractDir -Recurse -Filter 'ciadpi.exe' |
            Select-Object -First 1
        if (-not $binary) { throw 'ciadpi.exe was not found inside the downloaded ByeDPI archive.' }

        New-Item -ItemType Directory -Path $byeDpiDir -Force | Out-Null
        Copy-Item -LiteralPath $binary.FullName -Destination $byeDpiExe -Force
        Write-Host "    Installed: $byeDpiExe" -ForegroundColor Green
    }

    # --------------------------------------------------------------- ProxiFyre
    $proxiExe = Join-Path $proxiFyreDir 'ProxiFyre.exe'
    if ((Test-Path -LiteralPath $proxiExe) -and -not $Force) {
        Write-Host "ProxiFyre already present: $proxiExe (use -Force to re-download)" -ForegroundColor Yellow
    }
    else {
        Write-Step 'Resolving the latest ProxiFyre release'
        $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/wiresock/proxifyre/releases/latest' `
            -Headers @{ 'User-Agent' = 'byedpi-roblox-control' }

        $asset = $release.assets | Where-Object { $_.name -like 'ProxiFyre-v*-x64.zip' } | Select-Object -First 1
        if (-not $asset) { throw 'Could not find an x64 ZIP asset in the latest ProxiFyre release.' }

        Write-Host "    Version: $($release.tag_name)"
        Write-Host "    Asset  : $($asset.name)"

        $zipPath = Join-Path $tempRoot $asset.name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath

        New-Item -ItemType Directory -Path $proxiFyreDir -Force | Out-Null
        Expand-Archive -LiteralPath $zipPath -DestinationPath $proxiFyreDir -Force
        Write-Host "    Installed into: $proxiFyreDir" -ForegroundColor Green
    }

    Write-Host ''
    Write-Host 'Dependencies are ready. Next step:' -ForegroundColor Green
    Write-Host '    .\scripts\Install-RobloxServices.ps1   (as Administrator)'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
