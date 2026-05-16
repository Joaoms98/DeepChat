<#
.SYNOPSIS
  Builds self-contained single-file executables for the DeepChat Server and Client.

.DESCRIPTION
  Produces native win-x64 binaries that run on a clean Windows machine with no
  .NET runtime installed. Output goes to .\publish\server\ and .\publish\client\.

.PARAMETER Runtime
  Target runtime. Default win-x64. Use linux-x64 / osx-x64 if cross-publishing.

.PARAMETER Configuration
  Build configuration. Default Release.

.EXAMPLE
  .\scripts\1-publish.ps1
  .\scripts\1-publish.ps1 -Runtime linux-x64
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
# Script lives in scripts/, but every path below is relative to the repo root.
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$publishRoot = Join-Path $repoRoot "publish"
if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

function Publish-Project {
    param([string]$Project, [string]$OutputName)
    $outDir = Join-Path $publishRoot $OutputName
    Write-Host "==> Publishing $Project -> $outDir" -ForegroundColor Cyan
    dotnet publish $Project `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $Project" }
}

Publish-Project "src/Galileo.Chat.Server/Galileo.Chat.Server.csproj" "server"
Publish-Project "src/Galileo.Chat.Client/Galileo.Chat.Client.csproj" "client"

Write-Host ""
Write-Host "==> Done." -ForegroundColor Green
Write-Host "  Server: $(Join-Path $publishRoot 'server\Galileo.Chat.Server.exe')" -ForegroundColor Gray
Write-Host "  Client: $(Join-Path $publishRoot 'client\deepchat.exe')" -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Copy publish\server to the host machine; edit appsettings.Production.json"
Write-Host "     (set Network:AllowedIps, Jwt:SecretKey, Persistence:ConnectionString)"
Write-Host "  2. Set env DEEPCHAT_JWT_SECRET to a strong random string OR put it in"
Write-Host "     appsettings.Production.json (do NOT commit that file)."
Write-Host "  3. Generate a self-signed TLS cert; bind Kestrel to a LAN IP, NOT 0.0.0.0."
Write-Host "  4. Distribute deepchat.exe to clients with the server URL."
