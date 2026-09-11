<#
.SYNOPSIS
    Installs and configures ByeDPI (ciadpi) and ProxiFyre as Windows services
    so that Roblox traffic is routed through a local SOCKS5 proxy.

.DESCRIPTION
    This script performs the full setup:

      1. Validates that ciadpi.exe (ByeDPI) and ProxiFyre.exe are present.
      2. Registers ByeDPI as an auto-start Windows service with a DPI-bypass
         argument set tuned for Roblox.
      3. Writes the ProxiFyre app-config.json that redirects Roblox processes
         to the local SOCKS5 endpoint.
      4. Registers ProxiFyreService via the official Topshelf `install` command.
      5. Configures automatic restart-on-failure for both services.
      6. Starts both services and verifies the 127.0.0.1:1080 listener.

    Run this script from an elevated (Administrator) PowerShell session.

.PARAMETER ByeDpiExe
    Full path to ciadpi.exe. Defaults to %USERPROFILE%\.local\bin\ciadpi.exe

.PARAMETER ProxiFyreDir
    Folder containing ProxiFyre.exe. Defaults to
    %LOCALAPPDATA%\Programs\ProxiFyre

.PARAMETER ByeDpiArguments
    Argument string passed to ciadpi.exe when the service starts.

.EXAMPLE
    .\Install-RobloxServices.ps1

.EXAMPLE
    .\Install-RobloxServices.ps1 -ByeDpiExe 'D:\tools\ciadpi.exe'
#>
[CmdletBinding()]
param(
    [string]$ByeDpiExe = (Join-Path $env:USERPROFILE '.local\bin\ciadpi.exe'),
    [string]$ProxiFyreDir = (Join-Path $env:LOCALAPPDATA 'Programs\ProxiFyre'),
    [string]$ByeDpiArguments = '--ip 127.0.0.1 --split 1 --disorder 3+s --mod-http=h,d --auto=torst --tlsrec 1+s',
    [int]$SocksPort = 1080
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

$proxiFyreExe = Join-Path $ProxiFyreDir 'ProxiFyre.exe'
$configPath = Join-Path $ProxiFyreDir 'app-config.json'
$resultPath = Join-Path $ProxiFyreDir 'install-result.json'

# ---------------------------------------------------------------- validation
Write-Step 'Validating prerequisites'

if (-not (Test-Path -LiteralPath $ByeDpiExe)) {
    throw "ciadpi.exe was not found at '$ByeDpiExe'. Download ByeDPI (x86_64-w64 build) and pass -ByeDpiExe with the correct path."
}
if (-not (Test-Path -LiteralPath $proxiFyreExe)) {
    throw "ProxiFyre.exe was not found at '$proxiFyreExe'. Install ProxiFyre or pass -ProxiFyreDir with the correct folder."
}

Write-Host "    ByeDPI   : $ByeDpiExe"
Write-Host "    ProxiFyre: $proxiFyreExe"

# ------------------------------------------------------------ byeDPI service
Write-Step 'Registering the ByeDPI service'

# Remove any interactive ciadpi process so the service can own the port.
Get-Process ciadpi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$existingBye = Get-Service -Name 'ByeDPI' -ErrorAction SilentlyContinue
if ($existingBye) {
    if ($existingBye.Status -ne 'Stopped') {
        Stop-Service -Name 'ByeDPI' -Force
    }
    sc.exe delete ByeDPI | Out-Null
    Start-Sleep -Seconds 1
}

$byeBinaryPath = '"' + $ByeDpiExe + '" ' + $ByeDpiArguments
New-Service -Name 'ByeDPI' `
    -BinaryPathName $byeBinaryPath `
    -DisplayName 'ByeDPI Local SOCKS5 Proxy' `
    -Description 'Local-only SOCKS5 proxy with DPI desynchronization for Roblox.' `
    -StartupType Automatic | Out-Null

sc.exe failure ByeDPI reset= 86400 actions= restart/5000/restart/15000/none/0 | Out-Null
Start-Service -Name 'ByeDPI'
Write-Host '    Service ByeDPI started.' -ForegroundColor Green

# --------------------------------------------------------- proxifyre app cfg
Write-Step 'Writing the ProxiFyre application configuration'

$config = [ordered]@{
    logLevel  = 'Warning'
    bypassLan = $true
    proxies   = @(
        [ordered]@{
            appNames                = @(
                'RobloxPlayerBeta',
                'C:\Program Files\WindowsApps\ROBLOXCorporation.RobloxGDK'
            )
            socks5ProxyEndpoint     = "127.0.0.1:$SocksPort"
            socks5Transport         = 'TCP'
            supportedProtocols      = @('TCP', 'UDP')
            supportedAddressFamilies = @('IPv4', 'IPv6')
        }
    )
    excludes  = @()
}

$config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8
Write-Host "    Wrote $configPath"

# -------------------------------------------------------- proxifyre service
Write-Step 'Registering the ProxiFyre service'

$existingProxi = Get-CimInstance Win32_Service | Where-Object { $_.PathName -like '*ProxiFyre.exe*' }
if (-not $existingProxi) {
    Push-Location $ProxiFyreDir
    try {
        & $proxiFyreExe install | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "ProxiFyre service installation failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

$proxiService = Get-CimInstance Win32_Service |
    Where-Object { $_.PathName -like '*ProxiFyre.exe*' } |
    Select-Object -First 1

if (-not $proxiService) {
    throw 'The ProxiFyre service was not registered.'
}

Set-Service -Name $proxiService.Name -StartupType Automatic
sc.exe failure $proxiService.Name reset= 86400 actions= restart/5000/restart/15000/none/0 | Out-Null
if ((Get-Service -Name $proxiService.Name).Status -ne 'Running') {
    Start-Service -Name $proxiService.Name
}
Write-Host "    Service $($proxiService.Name) started." -ForegroundColor Green

# --------------------------------------------------------------- verification
Write-Step 'Verifying the installation'

Start-Sleep -Seconds 2

$byeService = Get-CimInstance Win32_Service -Filter "Name='ByeDPI'"
$proxiService = Get-CimInstance Win32_Service -Filter "Name='$($proxiService.Name)'"
$listener = Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort $SocksPort -State Listen -ErrorAction SilentlyContinue

$result = [pscustomobject]@{
    ByeDPIService    = [pscustomobject]@{
        Name      = $byeService.Name
        State     = $byeService.State
        StartMode = $byeService.StartMode
        PathName  = $byeService.PathName
    }
    ProxiFyreService = [pscustomobject]@{
        Name      = $proxiService.Name
        State     = $proxiService.State
        StartMode = $proxiService.StartMode
        PathName  = $proxiService.PathName
    }
    SocksListener    = @($listener | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" })
}

$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding UTF8
$result | ConvertTo-Json -Depth 5 | Write-Host

if ($result.SocksListener.Count -eq 0) {
    Write-Warning "No listener was detected on 127.0.0.1:$SocksPort. Try restarting the services from the control panel."
}
else {
    Write-Host 'Installation completed successfully.' -ForegroundColor Green
}
