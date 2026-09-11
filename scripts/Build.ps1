<#
.SYNOPSIS
    Builds and publishes ByeDPIControl.exe into the dist folder.

.DESCRIPTION
    Runs `dotnet publish` for the WinForms project and copies the resulting
    single-file executable to <projectRoot>\dist\ByeDPIControl.exe, which is
    the location the desktop-shortcut helper looks for.

.PARAMETER SelfContained
    Produce a self-contained build that does not require a separate
    .NET Desktop Runtime installation (larger file).

.PARAMETER SkipCopy
    Do not copy the executable into the dist folder.

.EXAMPLE
    .\Build.ps1

.EXAMPLE
    .\Build.ps1 -SelfContained
#>
[CmdletBinding()]
param(
    [switch]$SelfContained,
    [switch]$SkipCopy
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'Source\ByeDPIControl.csproj'

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project file not found at '$project'."
}

$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
$publishDir = Join-Path $projectRoot 'Source\bin\Release\net9.0-windows\win-x64\publish'

Write-Host '==> Publishing ByeDPIControl' -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained $selfContainedValue `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$produced = Join-Path $publishDir 'ByeDPIControl.exe'
if (-not (Test-Path -LiteralPath $produced)) {
    throw "Expected output was not produced at '$produced'."
}

if (-not $SkipCopy) {
    $distDir = Join-Path $projectRoot 'dist'
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
    Copy-Item -LiteralPath $produced -Destination (Join-Path $distDir 'ByeDPIControl.exe') -Force
    Write-Host "Copied to $(Join-Path $distDir 'ByeDPIControl.exe')" -ForegroundColor Green
}

Write-Host 'Build complete.' -ForegroundColor Green
