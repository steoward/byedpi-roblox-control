<#
.SYNOPSIS
    Adds a Windows Firewall rule that lets ProxiFyre redirect Roblox traffic
    and then restarts the ProxiFyre service.

.DESCRIPTION
    ProxiFyre performs process-specific local redirection. Without an inbound
    allow rule for its executable, redirected traffic can be dropped.

    Run this script from an elevated (Administrator) PowerShell session.

.PARAMETER ProxiFyreDir
    Folder containing ProxiFyre.exe. Defaults to
    %LOCALAPPDATA%\Programs\ProxiFyre

.EXAMPLE
    .\Configure-Firewall.ps1
#>
[CmdletBinding()]
param(
    [string]$ProxiFyreDir = (Join-Path $env:LOCALAPPDATA 'Programs\ProxiFyre')
)

$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must be run as Administrator. Right-click PowerShell and choose "Run as administrator".'
    }
}

Assert-Administrator

$ruleName = 'ProxiFyre Local Redirect'
$program = Join-Path $ProxiFyreDir 'ProxiFyre.exe'
$resultPath = Join-Path $ProxiFyreDir 'firewall-result.json'

if (-not (Test-Path -LiteralPath $program)) {
    throw "ProxiFyre.exe was not found at '$program'. Install ProxiFyre first, or pass -ProxiFyreDir."
}

Write-Host "==> Configuring firewall rule '$ruleName'" -ForegroundColor Cyan

Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule

New-NetFirewallRule -DisplayName $ruleName `
    -Description 'Allow ProxiFyre process-specific local TCP/UDP redirection for Roblox.' `
    -Direction Inbound `
    -Program $program `
    -Action Allow `
    -Profile Any `
    -Protocol Any | Out-Null

Write-Host '    Restarting ProxiFyre service' -ForegroundColor Cyan
Restart-Service -Name 'ProxiFyreService' -Force
Start-Sleep -Seconds 2

$rule = Get-NetFirewallRule -DisplayName $ruleName
$svc = Get-CimInstance Win32_Service -Filter "Name='ProxiFyreService'"

$result = [pscustomobject]@{
    FirewallRule = $rule.DisplayName
    Enabled      = $rule.Enabled.ToString()
    Action       = $rule.Action.ToString()
    ServiceState = $svc.State
    ServicePID   = $svc.ProcessId
}

$result | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
$result | ConvertTo-Json | Write-Host

Write-Host 'Firewall configuration complete.' -ForegroundColor Green
