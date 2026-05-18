<#
.SYNOPSIS
  One-command bootstrap for the DeepChat server. Run this on the machine that
  will host the chat - it figures out the current LAN IP, updates the client
  config + ZIP so guests connect to the right address, forces the active
  Wi-Fi profile to Private (if invoked elevated), and finally starts the
  published server bound to 0.0.0.0:5666.

.DESCRIPTION
  Solves the recurring pain of "I changed Wi-Fi networks, the client ZIP is
  stale, and Windows defaulted the new network to Public so nobody can reach
  me." Idempotent - safe to re-run on the same network.

.PARAMETER Port
  TCP port the server binds. Defaults to 5666 (matches firewall rule we ship).

.PARAMETER NoServer
  Only refresh config + ZIP. Don't actually start the server.

.EXAMPLE
  .\scripts\2-host-server.ps1            # Refresh and start
  .\scripts\2-host-server.ps1 -NoServer  # Just refresh artefacts
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

# --- 2. Materialize publish\share (idempotent) ---------------------------
# 1-publish wipes publish/ on every run, so share/ doesn't survive. We rebuild
# it here from publish\client\deepchat.exe + a fresh appsettings + LEIAME.
$repoRoot = Split-Path -Parent $PSScriptRoot
$share = Join-Path $repoRoot 'publish\share'
$clientExeSrc = Join-Path $repoRoot 'publish\client\deepchat.exe'

if (-not (Test-Path $clientExeSrc)) {
    throw "publish\client\deepchat.exe not found - run .\scripts\1-publish.ps1 first."
}

New-Item -ItemType Directory -Force -Path $share | Out-Null
Copy-Item -Path $clientExeSrc -Destination (Join-Path $share 'deepchat.exe') -Force

$appsettings = Join-Path $share 'appsettings.json'
Write-Step "Writing $appsettings"
@{
    Client = @{
        ServerUrl        = $serverUrl
        DefaultRoom      = 'backend'
        AllowInsecureTls = $true
    }
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $appsettings -Encoding UTF8
Write-Ok "ServerUrl -> $serverUrl"

$leiame = Join-Path $share 'LEIAME.txt'
if (-not (Test-Path $leiame)) {
    @"
DeepChat - Cliente LAN
======================

1. Mantenha deepchat.exe e appsettings.json na MESMA pasta.
2. Double-click em deepchat.exe.
   (SmartScreen pode alertar - 'Mais informacoes' -> 'Executar mesmo assim'.)
3. Menu inicial: Registrar (1a vez) ou Entrar.
4. Escolha sala existente ou crie uma. Passphrase combinada por fora.
5. Compare o 'Fingerprint da chave' com a pessoa que te convidou.

A passphrase NUNCA passa pelo servidor. Senha de login e passphrase
sao coisas separadas. Historico de mensagens apaga em 24h.
"@ | Set-Content -LiteralPath $leiame -Encoding UTF8
    Write-Ok "Created $leiame"
}

# --- 3. Re-zip distribution package --------------------------------------
$zip = Join-Path $repoRoot 'publish\deepchat-cliente.zip'
Write-Step "Repacking $zip"
Compress-Archive -Path (Join-Path $share '*') -DestinationPath $zip -Force
$size = [math]::Round((Get-Item $zip).Length / 1MB, 2)
Write-Ok "$zip ($size MB)"

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
    throw "Server EXE not found at $serverExe - run .\scripts\1-publish.ps1 first."
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
