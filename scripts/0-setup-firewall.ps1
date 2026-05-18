<#
.SYNOPSIS
  Liberates inbound TCP 5666 in Windows Defender Firewall. Run ONCE per host
  machine, in an ELEVATED PowerShell (Win+X -> Terminal Admin). The rule is
  persistent - won't need to re-run after reboot or network change.

.PARAMETER Port
  Listening port. Default 5666 (matches scripts/2-host-server.ps1).
#>
[CmdletBinding()]
param([int]$Port = 5666)

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "This script must run in an ELEVATED PowerShell." -ForegroundColor Red
    Write-Host "  Win+X -> 'Terminal (Admin)' or 'Windows PowerShell (Admin)', then re-run." -ForegroundColor Yellow
    exit 1
}

# Idempotent: remove an old rule first (e.g. from a prior port) so we don't pile up.
Remove-NetFirewallRule -DisplayName 'DeepChat' -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName 'DeepChat' -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null

Write-Host "OK: inbound TCP $Port allowed (rule 'DeepChat')." -ForegroundColor Green
