# Launches the DeepChat Spectre.Console client.
# Runs from any cwd: anchors itself to the repo root (one level above scripts/)
# so .csproj resolution is stable. Compiles once if needed, then runs with
# --no-build to skip the per-launch incremental build (saves ~1s per start).

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

$project = Join-Path $repoRoot 'src\Galileo.Chat.Client\Galileo.Chat.Client.csproj'
$assembly = Join-Path $repoRoot 'src\Galileo.Chat.Client\bin\Debug\net8.0\deepchat.dll'

if (-not (Test-Path $assembly)) {
    Write-Host '› Building client (first run)...' -ForegroundColor DarkGray
    dotnet build $project -c Debug --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

dotnet run --project $project --no-build
