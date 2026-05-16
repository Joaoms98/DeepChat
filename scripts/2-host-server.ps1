<#
.SYNOPSIS
  One-command bootstrap for the DeepChat server. Run this on the machine that
  will host the chat — it figures out the current LAN IP, updates the client
  config + ZIP so guests connect to the right address, forces the active
  Wi-Fi profile to Private (if invoked elevated), and finally starts the
  published server bound to 0.0.0.0:5666.

.DESCRIPTION
  Solves the recurring pain of "I changed Wi-Fi networks, the client ZIP is
  stale, and Windows defaulted the new network to Public so nobody can reach
  me." Idempotent — safe to re-run on the same network.

.PARAMETER Port
  TCP port the server binds. Defaults to 5666 (matches firewall rule we ship).

.PARAMETER NoServer
  Only refresh config + ZIP. Don't actually start the server.

.EXAMPLE
  .\scripts\host-server.ps1            # Refresh and start
  .\scripts\host-server.ps1 -NoServer  # Just refresh artefacts
#>

[CmdletBinding()]
param(
    [int]$Port = 5666,
    [switch]$NoServer
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    WARN: $msg" -ForegroundColor Yellow }

# --- 1. LAN IP discovery -------------------------------------------------
Write-Step "Discovering LAN IP"
$ip = (Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
        $_.InterfaceAlias -notmatch 'Loopback|vEthernet|WSL|Hyper-V' -and
        $_.IPAddress -notlike '169.254.*' -and
        $_.IPAddress -ne '127.0.0.1'
    } |
    Select-Object -First 1).IPAddress

if (-not $ip) {
    throw "Couldn't find a usable LAN IPv4. Are you connected to a network?"
}
Write-Ok "Detected $ip"
$serverUrl = "http://${ip}:$Port"

# --- 2. Update client appsettings.json -----------------------------------
$share = Join-Path $PSScriptRoot '..\publish\share'
$share = [System.IO.Path]::GetFullPath($share)
$appsettings = Join-Path $share 'appsettings.json'

if (-not (Test-Path $appsettings)) {
    Write-Warn "publish\share\appsettings.json missing — was the client published yet?"
} else {
    Write-Step "Updating client appsettings.json"
    $json = Get-Content $appsettings -Raw | ConvertFrom-Json
    $json.Client.ServerUrl = $serverUrl
    ($json | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $appsettings -Encoding UTF8
    Write-Ok "ServerUrl -> $serverUrl"
}

# --- 3. Re-zip distribution package --------------------------------------
$zip = Join-Path $PSScriptRoot '..\publish\deepchat-cliente.zip'
$zip = [System.IO.Path]::GetFullPath($zip)
if (Test-Path $share) {
    Write-Step "Repacking $zip"
    Compress-Archive -Path (Join-Path $share '*') -DestinationPath $zip -Force
    $size = [math]::Round((Get-Item $zip).Length / 1MB, 2)
    Write-Ok "$zip (${size} MB)"
} else {
    Write-Warn "Skipping ZIP — share folder not found."
}

# --- 4. Force the active network profile to Private ----------------------
# Default Windows behaviour: every new Wi-Fi starts as Public, which makes
# Defender drop inbound traffic on the user-defined allow rule. We flip the
# active profile to Private if we have admin; otherwise we just warn.
Write-Step "Checking active network profile"
$active = Get-NetConnectionProfile | Where-Object { $_.IPv4Connectivity -eq 'Internet' } | Select-Object -First 1
if ($active) {
    Write-Host "    Active: $($active.Name) ($($active.InterfaceAlias)) -> $($active.NetworkCategory)"
    if ($active.NetworkCategory -ne 'Private') {
        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if ($isAdmin) {
            Set-NetConnectionProfile -InterfaceAlias $active.InterfaceAlias -NetworkCategory Private
            Write-Ok "Flipped network to Private."
        } else {
            Write-Warn "Network is $($active.NetworkCategory). Re-run this script in an ADMIN PowerShell to fix it, or run manually:"
            Write-Host "      Set-NetConnectionProfile -InterfaceAlias `"$($active.InterfaceAlias)`" -NetworkCategory Private" -ForegroundColor Yellow
        }
    } else {
        Write-Ok "Already Private."
    }
} else {
    Write-Warn "No active internet profile detected."
}

# --- 5. Firewall rule sanity-check ---------------------------------------
Write-Step "Checking firewall rule"
$rule = Get-NetFirewallRule -DisplayName 'DeepChat' -ErrorAction SilentlyContinue
if (-not $rule) {
    Write-Warn "Firewall rule 'DeepChat' missing. Run ONCE in admin PowerShell:"
    Write-Host "      .\scripts\0-setup-firewall.ps1" -ForegroundColor Yellow
} else {
    $ports = ($rule | Get-NetFirewallPortFilter).LocalPort
    if ($ports -notcontains "$Port") {
        Write-Warn "Firewall rule exists but for port(s) [$ports], not $Port. Recreate it for $Port."
    } else {
        Write-Ok "Inbound TCP $Port allowed."
    }
}

# --- 6. Start the server -------------------------------------------------
if ($NoServer) {
    Write-Step "Skipping server start (-NoServer)"
    Write-Host ""
    Write-Host "Hand the ZIP to guests: $zip" -ForegroundColor Green
    Write-Host "Guests connect to:      $serverUrl" -ForegroundColor Green
    return
}

$serverExe = Join-Path $PSScriptRoot '..\publish\server\Galileo.Chat.Server.exe'
$serverExe = [System.IO.Path]::GetFullPath($serverExe)
if (-not (Test-Path $serverExe)) {
    throw "Server EXE not found at $serverExe — run .\scripts\1-publish.ps1 first."
}

# Free the port if a previous server is still bound.
$inUse = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
if ($inUse) {
    Write-Warn "Port $Port already bound by PID $($inUse.OwningProcess). Killing it."
    Stop-Process -Id $inUse.OwningProcess -Force
    Start-Sleep -Milliseconds 800
}

Write-Step "Starting server"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$serverDir = Split-Path -Parent $serverExe
Push-Location $serverDir
try {
    & $serverExe --urls "http://0.0.0.0:$Port"
} finally {
    Pop-Location
}
