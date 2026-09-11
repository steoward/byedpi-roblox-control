<#
.SYNOPSIS
    Removes the ByeDPI and ProxiFyre Windows services installed by
    Install-RobloxServices.ps1.

.DESCRIPTION
    Stops and deletes both services, removes the ProxiFyre firewall rule
    created by Configure-Firewall.ps1, and reports what was removed.
    Installed binaries and configuration files are left untouched unless
    -RemoveConfig is specified.

    Run this script from an elevated (Administrator) PowerShell session.

.PARAMETER RemoveConfig
    Also delete the generated ProxiFyre app-config.json and result files.

.EXAMPLE
    .\Uninstall-RobloxServices.ps1

.EXAMPLE
    .\Uninstall-RobloxServices.ps1 -RemoveConfig
#>
[CmdletBinding()]
param(
    [string]$ProxiFyreDir = (Join-Path $env:LOCALAPPDATA 'Programs\ProxiFyre'),
    [switch]$RemoveConfig
)

$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must be run as Administrator. Right-click PowerShell and choose "Run as administrator".'
    }
}

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

Assert-Administrator

$removed = [System.Collections.Generic.List[string]]::new()

# ------------------------------------------------------------- ProxiFyre first
Write-Step 'Stopping and removing the ProxiFyre service'

$proxiService = Get-CimInstance Win32_Service |
    Where-Object { $_.PathName -like '*ProxiFyre.exe*' } |
    Select-Object -First 1

if ($proxiService) {
    $proxiExe = Join-Path $ProxiFyreDir 'ProxiFyre.exe'

    if ((Get-Service -Name $proxiService.Name -ErrorAction SilentlyContinue).Status -ne 'Stopped') {
        Stop-Service -Name $proxiService.Name -Force -ErrorAction SilentlyContinue
    }

    # Prefer the official Topshelf uninstall so its own cleanup runs.
    if (Test-Path -LiteralPath $proxiExe) {
        Push-Location $ProxiFyreDir
        try { & $proxiExe uninstall 2>&1 | Out-Null } catch { }
        finally { Pop-Location }
    }

    # Fall back to sc.exe delete if the service is still registered.
    if (Get-Service -Name $proxiService.Name -ErrorAction SilentlyContinue) {
        sc.exe delete $proxiService.Name | Out-Null
    }

    $removed.Add("Service: $($proxiService.Name)")
    Write-Host "    Removed $($proxiService.Name)" -ForegroundColor Green
}
else {
    Write-Host '    ProxiFyre service was not registered; skipping.'
}

# ----------------------------------------------------------------- byeDPI next
Write-Step 'Stopping and removing the ByeDPI service'

$byeService = Get-Service -Name 'ByeDPI' -ErrorAction SilentlyContinue
if ($byeService) {
    if ($byeService.Status -ne 'Stopped') {
        Stop-Service -Name 'ByeDPI' -Force -ErrorAction SilentlyContinue
    }
    sc.exe delete ByeDPI | Out-Null
    $removed.Add('Service: ByeDPI')
    Write-Host '    Removed ByeDPI' -ForegroundColor Green
}
else {
    Write-Host '    ByeDPI service was not registered; skipping.'
}

# Any leftover interactive process would keep the port bound.
Get-Process ciadpi -ErrorAction SilentlyContinue | Stop-Process -Force

# --------------------------------------------------------------- firewall rule
Write-Step 'Removing the ProxiFyre firewall rule'

$ruleName = 'ProxiFyre Local Redirect'
$rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($rule) {
    Remove-NetFirewallRule -DisplayName $ruleName
    $removed.Add("Firewall rule: $ruleName")
    Write-Host "    Removed '$ruleName'" -ForegroundColor Green
}
else {
    Write-Host '    Firewall rule was not present; skipping.'
}

# ------------------------------------------------------------- config cleanup
if ($RemoveConfig -and (Test-Path -LiteralPath $ProxiFyreDir)) {
    Write-Step 'Removing generated configuration files'
    foreach ($name in 'app-config.json', 'install-result.json', 'firewall-result.json') {
        $target = Join-Path $ProxiFyreDir $name
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Force
            $removed.Add("File: $target")
            Write-Host "    Removed $target" -ForegroundColor Green
        }
    }
}

Write-Host ''
if ($removed.Count -eq 0) {
    Write-Host 'Nothing was installed, so nothing was removed.' -ForegroundColor Yellow
}
else {
    Write-Host 'Uninstall complete. Removed:' -ForegroundColor Green
    $removed | ForEach-Object { Write-Host "  - $_" }
}
