[CmdletBinding()]
param(
    [switch]$Playtest,
    [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'server\NWO.Server\NWO.Server.csproj'
$client = Join-Path $repoRoot 'client'
$dataDir = Join-Path $repoRoot '.data'
$db = Join-Path $dataDir ($(if ($Playtest) { 'playtest.db' } else { 'development.db' }))

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'The .NET 8 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/8.0'
}

$sdks = & $dotnet.Source --list-sdks
if (-not ($sdks -match '^8\.')) {
    throw 'The dotnet command is available, but the .NET 8 SDK is not installed.'
}

New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:NWO_DB = $db
$env:NWO_CLIENT = $client

if ($Playtest) {
    $env:NWO_DEV_TOOLS = '1'
    $env:NWO_START_CASH = '15000'
    $env:NWO_START_FUEL = '300'
    $env:NWO_START_GOLD = '100'
    $env:NWO_RESEARCH_SPEED = '60'
    $env:NWO_ELECTION_MINUTES = '15'
    $env:NWO_LAW_MINUTES = '5'
    $env:NWO_SENTINEL_AFTER_HOURS = '0.25'
    $env:NWO_SENTINEL_RETRY_MINUTES = '10'
    $env:NWO_SENTINEL_HP_MULT = '0.10'
    $env:NWO_SENTINEL_FULL_ARMOR = '2'
    $env:NWO_BOTS_PER_SECTOR = '2'
}

Write-Host "Starting New World Order at http://localhost:$Port"
Write-Host "Database: $db"
Write-Host "Profile: $(if ($Playtest) { 'playtest' } else { 'development' })"

& $dotnet.Source run --project $project --urls "http://localhost:$Port"
