<#
.SYNOPSIS
    Creates a desktop shortcut named "تحكم Roblox" that launches
    ByeDPIControl.exe.

.DESCRIPTION
    Publishes nothing and installs nothing: it only writes a .lnk file to the
    current user's desktop. If the target executable is missing, the script
    explains how to build it first.

.PARAMETER ExePath
    Full path to ByeDPIControl.exe. Defaults to the copy next to this script's
    parent folder, then to the published release folder.

.PARAMETER ShortcutName
    Name of the shortcut file (without the .lnk extension).

.EXAMPLE
    .\New-DesktopShortcut.ps1

.EXAMPLE
    .\New-DesktopShortcut.ps1 -ExePath 'C:\Tools\ByeDPI Control\ByeDPIControl.exe'
#>
[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$ShortcutName = 'تحكم Roblox'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot

if (-not $ExePath) {
    $candidates = @(
        (Join-Path $projectRoot 'ByeDPIControl.exe'),
        (Join-Path $projectRoot 'dist\ByeDPIControl.exe'),
        (Join-Path $projectRoot 'Source\bin\Release\net9.0-windows\win-x64\publish\ByeDPIControl.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ }

    if ($candidates.Count -eq 0) {
        throw @"
ByeDPIControl.exe was not found. Build or download it first, for example:

    dotnet publish Source\ByeDPIControl.csproj -c Release -r win-x64 --self-contained false

or download the release ZIP from the repository Releases page, then pass
-ExePath pointing at the extracted ByeDPIControl.exe.
"@
    }

    $ExePath = $candidates[0]
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "The executable was not found at '$ExePath'."
}

$workDir = Split-Path -Parent (Resolve-Path -LiteralPath $ExePath).Path
$desktop = [Environment]::GetFolderPath('Desktop')
$linkPath = Join-Path $desktop "$ShortcutName.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($linkPath)
$shortcut.TargetPath = (Resolve-Path -LiteralPath $ExePath).Path
$shortcut.WorkingDirectory = $workDir
$shortcut.IconLocation = "$((Resolve-Path -LiteralPath $ExePath).Path),0"
$shortcut.Description = 'مركز تحكم Roblox - إدارة ByeDPI وProxiFyre'
$shortcut.Save()

Write-Host "Shortcut created: $linkPath" -ForegroundColor Green
Write-Host "Target          : $((Resolve-Path -LiteralPath $ExePath).Path)"
