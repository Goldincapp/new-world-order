[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080'
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

$health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -Method Get
if (-not $health.ok) { throw 'Health endpoint did not report ok.' }

$name = 'Smoke_' + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$body = @{ name = $name } | ConvertTo-Json
$guest = Invoke-RestMethod -Uri "$BaseUrl/api/auth/guest" -Method Post -ContentType 'application/json' -Body $body
if (-not $guest.token) { throw 'Guest creation did not return a token.' }

$headers = @{ Authorization = "Bearer $($guest.token)" }
$me = Invoke-RestMethod -Uri "$BaseUrl/api/me" -Method Get -Headers $headers
if ($me.player.name -ne $name) { throw 'Authenticated player did not match the created guest.' }
if (-not $me.player.home) { throw 'Created player did not receive a home base.' }
if (-not $me.player.parcels) { throw 'Created player did not receive starter land.' }

Write-Host "Smoke test passed for $name"
Write-Host "Home sector: $($me.player.homeSector)"
Write-Host "Starting cash: $($me.player.resources.cash)"
