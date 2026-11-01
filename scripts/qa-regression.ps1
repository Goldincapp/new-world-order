[CmdletBinding()]
param(
    [int]$Port = 5093,
    [switch]$KeepArtifacts,
    [switch]$CaptureVisuals,
    [switch]$BalanceReport,
    [switch]$TutorialE2E,
    [switch]$MidgameE2E,
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$tmpRoot = Join-Path $repoRoot '.tmp'
$runDir = Join-Path $tmpRoot ('qa-regression-' + [Guid]::NewGuid().ToString('N'))
$buildDir = Join-Path $runDir 'build'
$dbPath = Join-Path $runDir 'qa.db'
$outLog = Join-Path $runDir 'server.out.log'
$errLog = Join-Path $runDir 'server.err.log'
$serverProcess = $null
$baseUrl = "http://127.0.0.1:$Port"

function Assert-QA($Condition, [string]$Message) {
    if ($Condition -is [array]) {
        $caller = (Get-PSCallStack | Select-Object -Skip 1 -First 1)
        throw "QA assertion received $($Condition.Count) values at line $($caller.ScriptLineNumber): $Message"
    }
    if (-not [bool]$Condition) { throw "QA assertion failed: $Message" }
}

function Invoke-QA([string]$Path, [string]$Method = 'Get', $Body = $null) {
    $args = @{ Uri = "$baseUrl$Path"; Method = $Method; Headers = $script:headers }
    if ($null -ne $Body) {
        $args.ContentType = 'application/json'
        $args.Body = $Body | ConvertTo-Json -Depth 8
    }
    Invoke-RestMethod @args
}

function Load-Checkpoint([string]$Scenario) {
    $result = Invoke-QA '/api/dev/qa/load' 'Post' @{ scenario = $Scenario }
    Assert-QA $result.ok "checkpoint '$Scenario' returned an error: $($result.error)"
    (Invoke-QA '/api/me').player
}

New-Item -ItemType Directory -Force -Path $runDir | Out-Null

try {
    $localDotnet = Join-Path $tmpRoot 'dotnet8\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet) { $dotnet = (Resolve-Path $localDotnet).Path }
    else {
        $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnetCommand) { throw 'The .NET 8 SDK is required to run QA regression tests.' }
        $dotnet = $dotnetCommand.Source
    }

    $project = Join-Path $repoRoot 'server\NWO.Server\NWO.Server.csproj'
    $client = Join-Path $repoRoot 'client'
    $dotnetHome = Join-Path $tmpRoot 'dotnet-home'
    New-Item -ItemType Directory -Force -Path $dotnetHome | Out-Null
    $env:DOTNET_CLI_HOME = $dotnetHome

    Write-Host 'Building isolated QA server...'
    & $dotnet build $project -c Release -o $buildDir --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'QA server build failed.' }

    $env:ASPNETCORE_URLS = $baseUrl
    $env:NWO_DB = $dbPath
    $env:NWO_CLIENT = $client
    $env:NWO_DEV_TOOLS = '1'
    $env:NWO_BOTS_PER_SECTOR = '0'
    $qaKey = [Guid]::NewGuid().ToString('N')
    $env:NWO_QA_KEY = $qaKey
    $serverDll = Join-Path $buildDir 'NWO.Server.dll'
    $serverProcess = Start-Process -FilePath $dotnet -ArgumentList ('"' + $serverDll + '"') -WorkingDirectory $repoRoot -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru

    $ready = $false
    # A clean Windows runner can need more than a minute to migrate and seed the world.
    # Wait up to 150 seconds, but continue immediately as soon as health responds.
    for ($attempt = 0; $attempt -lt 300; $attempt++) {
        if ($serverProcess.HasExited) { break }
        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -TimeoutSec 2
            if ($health.ok) { $ready = $true; break }
        } catch {}
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) {
        $details = @()
        if (Test-Path $errLog) { $details += Get-Content $errLog -Tail 30 }
        if (Test-Path $outLog) { $details += Get-Content $outLog -Tail 30 }
        throw "Isolated QA server did not start.`n$($details -join "`n")"
    }

    $name = 'QA_' + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $guest = Invoke-RestMethod -Uri "$baseUrl/api/auth/guest" -Method Post -ContentType 'application/json' -Body (@{ name = $name } | ConvertTo-Json)
    Assert-QA ([bool]$guest.token) 'guest creation did not return a token'
    $script:headers = @{ Authorization = "Bearer $($guest.token)" }

    $catalog = Invoke-QA '/api/dev/qa/scenarios'
    Assert-QA ($catalog.scenarios.Count -eq 14) 'checkpoint catalog should contain fourteen scenarios'
    $balance = Invoke-QA '/api/dev/qa/balance'
    Assert-QA $balance.ok "economy balance guardrails failed: $((@($balance.checks | Where-Object { -not $_.pass }) | ForEach-Object { $_.label }) -join ', ')"
    Assert-QA ($balance.summary.failed -eq 0) 'economy balance report should have no failed guardrails'

    $p = Load-Checkpoint 'fresh'
    Assert-QA ($p.progression.level -eq 1) 'fresh commander should be level 1'
    Assert-QA ($p.home.tutorial.do -eq 'hqintro') 'fresh commander should begin at Headquarters introduction'
    Assert-QA ($p.home.tiles.Count -eq 0) 'fresh commander should not have constructed buildings'
    Assert-QA ($p.resources.cash -lt 25000) 'fresh commander should retain normal starting resources'

    $p = Load-Checkpoint 'laststand'
    Assert-QA ($p.home.tutorial.do -eq 'laststand') 'Last Stand checkpoint should select Survival 4/8'
    Assert-QA (@($p.home.tiles | Where-Object type -eq 'barracks').Count -eq 1) 'Last Stand checkpoint should include one Barracks'

    $p = Load-Checkpoint 'walls'
    Assert-QA ($p.home.tutorial.do -eq 'walls') 'walls checkpoint should select the gate reinforcement lesson'

    $p = Load-Checkpoint 'expansion'
    Assert-QA ($p.progression.level -eq 2) 'Expansion checkpoint should be level 2'
    Assert-QA ($p.guide.do -eq 'claim') 'Expansion checkpoint should select parcel claiming'

    $p = Load-Checkpoint 'extraction'
    Assert-QA ($p.guide.do -eq 'extraction') 'Extraction checkpoint should select the Operation lesson'
    Assert-QA ($p.operations.unlocked -contains 'extraction') 'Extraction Run should be unlocked at its checkpoint'

    $p = Load-Checkpoint 'research'
    Assert-QA ($p.home.hqLevel -eq 2) 'Research Lab checkpoint should have HQ level 2'
    Assert-QA ($p.guide.do -eq 'research') 'Research Lab checkpoint should select the research lesson'
    Assert-QA ($p.research.labCount -eq 0) 'Research Lab should not already be built at the build checkpoint'

    $p = Load-Checkpoint 'research-ready'
    Assert-QA ($p.research.labCount -eq 1) 'research-ready checkpoint should have one active lab'
    Assert-QA ($p.research.recommended.id -eq 'mining') 'Mining should be the guided first research choice'

    $p = Load-Checkpoint 'delivery'
    Assert-QA ($p.guide.do -eq 'ship') 'delivery checkpoint should select the first contract lesson'
    Assert-QA ($p.home.hqLevel -eq 3) 'delivery checkpoint should have HQ level 3'

    $p = Load-Checkpoint 'operations'
    Assert-QA ($null -eq $p.guide) 'Operations sandbox should have completed the Field Guide'
    Assert-QA ($p.home.hqLevel -eq 5) 'Operations sandbox should have HQ level 5'
    Assert-QA ($p.home.walls.level -eq 4) 'Operations sandbox should have walls level 4'
    Assert-QA ($p.operations.unlocked.Count -eq 3) 'Operations sandbox should unlock all three Operations'
    Assert-QA ($p.operations.levels.laststand -eq 10 -and $p.operations.levels.extraction -eq 10 -and $p.operations.levels.leviathan -eq 10) 'Operations sandbox threats should all be level 10'

    $grant = Invoke-QA '/api/dev/qa/resources' 'Post'
    Assert-QA $grant.ok 'resource grant should succeed'
    $p = (Invoke-QA '/api/me').player
    Assert-QA ($p.resources.cash -ge 250000 -and $p.resources.fuel -ge 25000) 'resource grant should supply the QA resource bundle'

    $timers = Invoke-QA '/api/dev/qa/timers' 'Post'
    Assert-QA $timers.ok 'timer completion should succeed when no timers are active'

    $p = Load-Checkpoint 'roadmap-foundation'
    $roadmap = Invoke-QA '/api/roadmap'
    Assert-QA ($roadmap.chapterNumber -eq 1 -and $roadmap.tracks.Count -eq 3) 'Foundation Roadmap should expose three tracks'
    Assert-QA (@($roadmap.tracks | Where-Object { -not $_.complete }).Count -eq 0) 'Foundation checkpoint should complete all three tracks'
    foreach ($track in @('growth', 'economy', 'defense')) {
        $claim = Invoke-QA "/api/roadmap/claim?track=$track" 'Post'
        Assert-QA $claim.ok "Foundation track '$track' should be claimable"
    }
    $p = (Invoke-QA '/api/me').player
    Assert-QA ($p.motorPoolGuide.do -eq 'motorpool') 'Defense reward should begin the Motor Pool handoff'
    Assert-QA (@($p.home.kinds | Where-Object { $_.type -eq 'motorpool' -and $_.introduced }).Count -eq 1) 'Motor Pool should be introduced after the Defense claim'
    $placed = Invoke-QA '/api/dev/qa/tutorial-action' 'Post' @{ action = 'PlaceHome'; type = 'motorpool'; x = 15; y = 13; rotate = $false }
    Assert-QA $placed.ok "Motor Pool placement should succeed: $($placed.error)"
    $finished = Invoke-QA '/api/dev/qa/timers' 'Post'
    Assert-QA $finished.ok 'Motor Pool construction timer should finish in QA'
    Assert-QA ($finished.player.motorPoolGuide.do -eq 'motorpool-train') 'Completed Motor Pool should request the first tank'
    $cashBefore = [double]$finished.player.resources.cash
    $fuelBefore = [double]$finished.player.resources.fuel
    $trained = Invoke-QA '/api/dev/qa/tutorial-action' 'Post' @{ action = 'TrainDefenders'; type = 'tank'; count = 1 }
    Assert-QA $trained.ok "First tank deployment should succeed: $($trained.error)"
    Assert-QA ($null -eq $trained.player.motorPoolGuide) 'First tank should complete the Motor Pool handoff'
    Assert-QA ([double]$trained.player.resources.cash -eq $cashBefore + 200) 'First tank should net +200 cash after its 300 cash deployment cost and 500 cash reward'
    Assert-QA ([double]$trained.player.resources.fuel -eq $fuelBefore + 50) 'First tank should award 50 fuel'
    Assert-QA ([string]::Join(' ', @($trained.player.operations.details.extraction.support)) -match 'Motor Pool 1') 'Extraction support should display the Motor Pool level'
    Assert-QA ([string]::Join(' ', @($trained.player.operations.details.leviathan.support)) -match 'Motor Pool 1') 'Leviathan support should display the Motor Pool level'

    $p = Load-Checkpoint 'roadmap-regional'
    $roadmap = Invoke-QA '/api/roadmap'
    Assert-QA ($roadmap.chapterNumber -eq 2 -and $roadmap.chapter -eq 'Regional Command') 'Regional checkpoint should open Roadmap Chapter 2'
    Assert-QA (@($roadmap.tracks | Where-Object { -not $_.complete }).Count -eq 0) 'Regional checkpoint should complete all three tracks'
    $xpBefore = [int]$p.progression.xp
    foreach ($track in @('capital', 'network', 'combined')) {
        $claim = Invoke-QA "/api/roadmap/claim?track=$track" 'Post'
        Assert-QA $claim.ok "Regional track '$track' should be claimable"
    }
    $p = (Invoke-QA '/api/me').player
    Assert-QA ([int]$p.progression.xp -eq $xpBefore + 300) 'Regional Command should award 300 XP without repeated Operation runs'

    $p = Load-Checkpoint 'roadmap-national'
    $roadmap = Invoke-QA '/api/roadmap'
    Assert-QA ($roadmap.chapterNumber -eq 3 -and $roadmap.chapter -eq 'National Authority') 'National checkpoint should open Roadmap Chapter 3'
    Assert-QA (@($roadmap.tracks | Where-Object { -not $_.complete }).Count -eq 0) 'National checkpoint should complete all three tracks'
    Assert-QA (-not [bool]$p.nationalCommand.unlocked) 'National Command should remain locked until every Chapter 3 reward is claimed'
    $xpBefore = [int]$p.progression.xp
    foreach ($track in @('territory', 'coalition', 'civic')) {
        $claim = Invoke-QA "/api/roadmap/claim?track=$track" 'Post'
        Assert-QA $claim.ok "National track '$track' should be claimable"
    }
    $p = (Invoke-QA '/api/me').player
    Assert-QA ([int]$p.progression.xp -eq $xpBefore + 450) 'National Authority should award 450 XP'
    Assert-QA ([bool]$p.nationalCommand.unlocked -and -not [bool]$p.nationalCommand.active) 'National Authority should unlock Command Center plans before its bonuses activate'
    Assert-QA ($p.commandCenterGuide.do -eq 'commandcenter') 'National Authority should begin the Command Center construction handoff'
    $placed = Invoke-QA '/api/dev/qa/tutorial-action' 'Post' @{ action = 'PlaceHome'; type = 'commandcenter'; x = 18; y = 13; rotate = $false }
    Assert-QA $placed.ok "National Command Center placement should succeed: $($placed.error)"
    $finished = Invoke-QA '/api/dev/qa/timers' 'Post'
    Assert-QA $finished.ok 'National Command Center construction timer should finish in QA'
    Assert-QA ($null -eq $finished.player.commandCenterGuide) 'Completed Command Center should finish its construction handoff'
    Assert-QA ([bool]$finished.player.nationalCommand.active -and [int]$finished.player.nationalCommand.productionBonus -eq 10) 'Level 1 National Command Center should activate a 10% production bonus'

    $p = Load-Checkpoint 'roadmap-network'
    $roadmap = Invoke-QA '/api/roadmap'
    Assert-QA ($roadmap.chapterNumber -eq 4 -and $roadmap.chapter -eq 'National Network') 'Network checkpoint should open Roadmap Chapter 4'
    Assert-QA (@($roadmap.tracks | Where-Object { -not $_.complete }).Count -eq 0) 'National Network checkpoint should complete all three tracks'
    foreach ($track in @('alliance-command', 'logistics-command', 'defense-command')) {
        $claim = Invoke-QA "/api/roadmap/claim?track=$track" 'Post'
        Assert-QA $claim.ok "National Network track '$track' should be claimable"
    }
    $p = (Invoke-QA '/api/me').player
    Assert-QA ([bool]$p.nationalCommand.networkAuthorized) 'Completing Chapter 4 should authorize the final Command Center upgrade'
    Assert-QA ($p.commandCenterGuide.do -eq 'commandcenter-final') 'Chapter 4 completion should begin the level 5 handoff'
    $command = @($p.home.tiles | Where-Object type -eq 'commandcenter')[0]
    $upgrade = Invoke-QA '/api/dev/qa/tutorial-action' 'Post' @{ action = 'UpgradeHome'; id = $command.id }
    Assert-QA $upgrade.ok "National Command Center level 5 upgrade should start: $($upgrade.error)"
    $finished = Invoke-QA '/api/dev/qa/timers' 'Post'
    Assert-QA $finished.ok 'National Command Center level 5 timer should finish in QA'
    Assert-QA ($null -eq $finished.player.commandCenterGuide) 'Level 5 should finish the National Network handoff'
    Assert-QA ([int]$finished.player.nationalCommand.level -eq 5 -and [int]$finished.player.nationalCommand.productionBonus -eq 15) 'Completed National Network should activate the 15% production bonus'

    $p = Load-Checkpoint 'roadmap-ashlands'
    $roadmap = Invoke-QA '/api/roadmap'
    Assert-QA ($roadmap.chapterNumber -eq 5 -and $roadmap.chapter -eq 'Ashlands Campaign') 'Ashlands checkpoint should open Roadmap Chapter 5'
    Assert-QA (@($roadmap.tracks | Where-Object { -not $_.complete }).Count -eq 0) 'Ashlands checkpoint should complete all three tracks'
    Assert-QA (-not [bool]$p.ashlandsCommand.chartered) 'Ashlands Charter should remain inactive before Chapter 5 rewards are claimed'
    $oilBefore = [double]$p.ratesPerHour.oil
    foreach ($track in @('frontier', 'foothold', 'sovereignty')) {
        $claim = Invoke-QA "/api/roadmap/claim?track=$track" 'Post'
        Assert-QA $claim.ok "Ashlands Campaign track '$track' should be claimable"
    }
    $p = (Invoke-QA '/api/me').player
    Assert-QA ([bool]$p.ashlandsCommand.chartered -and [int]$p.ashlandsCommand.productionBonus -eq 10) 'Chapter 5 should activate the Ashlands Charter'
    Assert-QA ([double]$p.ratesPerHour.oil -gt $oilBefore) 'Ashlands Charter should increase real frontier production'

    Write-Host ''
    Write-Host "QA regression passed: 14 checkpoints, Motor Pool and five Roadmap chapters, resource controls, timer controls, and $($balance.summary.total) balance guardrails." -ForegroundColor Green

    if ($BalanceReport) {
        $balanceOut = Join-Path $repoRoot 'artifacts\balance-audit'
        New-Item -ItemType Directory -Force -Path $balanceOut | Out-Null
        $balance | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $balanceOut 'report.json') -Encoding utf8
        $rows = @($balance.checks | ForEach-Object {
            $range = if ($null -ne $_.min -and $null -ne $_.max) { "$($_.min)-$($_.max)" } elseif ($null -ne $_.min) { ">= $($_.min)" } else { "<= $($_.max)" }
            '<tr class="' + $(if ($_.pass) { 'pass' } else { 'fail' }) + '"><td>' + $_.area + '</td><td><b>' + $_.label + '</b><small>' + $_.detail + '</small></td><td>' + $_.value + ' ' + $_.unit + '</td><td>' + $range + '</td><td>' + $(if ($_.pass) { 'PASS' } else { 'FAIL' }) + '</td></tr>'
        }) -join "`n"
        $html = '<!doctype html><html><head><meta charset="utf-8"><title>Nations Reborn balance audit</title><style>body{margin:0;background:#08131a;color:#e9f1f4;font:15px system-ui}header{padding:24px;background:#102630;border-bottom:1px solid #d5a92b}h1{margin:0;color:#ffd86a}header p{margin:.4em 0 0;color:#abc1c9}main{padding:24px}table{width:100%;border-collapse:collapse;background:#10212a;border:1px solid #31505c}th,td{padding:12px;text-align:left;border-bottom:1px solid #29434e;vertical-align:top}th{color:#9db5bf;text-transform:uppercase;font-size:.75em;letter-spacing:.08em}td small{display:block;color:#93aab3;margin-top:.25em;max-width:60em}.pass td:last-child{color:#8bea9b;font-weight:800}.fail{background:#481d22}.fail td:last-child{color:#ff8d91;font-weight:800}</style></head><body><header><h1>Nations Reborn balance audit</h1><p>' + $balance.summary.passed + ' of ' + $balance.summary.total + ' guardrails passed · generated ' + $balance.generatedAt + '</p></header><main><table><thead><tr><th>Area</th><th>Guardrail</th><th>Current</th><th>Target</th><th>Status</th></tr></thead><tbody>' + $rows + '</tbody></table></main></body></html>'
        Set-Content -LiteralPath (Join-Path $balanceOut 'report.html') -Value $html -Encoding utf8
        Write-Host "Balance report: $(Join-Path $balanceOut 'report.html')"
    }

    if ($TutorialE2E) {
        $tutorialTool = Join-Path $repoRoot 'tools\tutorial-e2e.mjs'
        $tutorialOut = Join-Path $repoRoot 'artifacts\tutorial-e2e'
        $tutorialArgs = @($tutorialTool, '--base-url', $baseUrl, '--token', $guest.token, '--out', $tutorialOut, '--profile', (Join-Path $runDir 'tutorial-edge-profile'))
        Write-Host 'Driving Survival and the opening Expansion journey in Edge...'
        & node @tutorialArgs
        if ($LASTEXITCODE -ne 0) { throw 'Tutorial browser test failed. Open artifacts\tutorial-e2e\report.html for the last completed step.' }
    }

    if ($MidgameE2E) {
        $midgameTool = Join-Path $repoRoot 'tools\midgame-e2e.mjs'
        $midgameOut = Join-Path $repoRoot 'artifacts\midgame-e2e'
        $midgameArgs = @($midgameTool, '--base-url', $baseUrl, '--token', $guest.token, '--out', $midgameOut, '--profile', (Join-Path $runDir 'midgame-edge-profile'))
        Write-Host 'Driving Motor Pool, Regional Command, and National Authority progression in Edge...'
        & node @midgameArgs
        if ($LASTEXITCODE -ne 0) { throw 'Midgame browser test failed. Open artifacts\midgame-e2e\report.html for the last completed step.' }
    }

    if ($CaptureVisuals) {
        $visualTool = Join-Path $repoRoot 'tools\visual-capture-edge.mjs'
        $visualOut = Join-Path $repoRoot 'artifacts\visual-regression'
        $visualArgs = @($visualTool, '--base-url', $baseUrl, '--commander', $name, '--qa-key', $qaKey, '--out', $visualOut, '--profile', (Join-Path $runDir 'edge-profile'))
        if ($UpdateBaseline) { $visualArgs += '--update-baseline' }
        Write-Host 'Capturing visual checkpoints...'
        & node @visualArgs
        if ($LASTEXITCODE -ne 0) { throw 'Visual checkpoint capture failed.' }
    }
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -ErrorAction SilentlyContinue
        $serverProcess.WaitForExit(5000) | Out-Null
    }
    if (-not $KeepArtifacts -and (Test-Path -LiteralPath $runDir)) {
        $resolvedTmp = (Resolve-Path $tmpRoot).Path.TrimEnd('\') + '\'
        $resolvedRun = (Resolve-Path $runDir).Path
        if (-not $resolvedRun.StartsWith($resolvedTmp, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove QA artifacts outside $resolvedTmp"
        }
        $removed = $false
        for ($cleanupAttempt = 0; $cleanupAttempt -lt 10; $cleanupAttempt++) {
            try {
                Remove-Item -LiteralPath $resolvedRun -Recurse -Force
                $removed = $true
                break
            } catch {
                if ($cleanupAttempt -eq 9) { throw }
                Start-Sleep -Milliseconds 500
            }
        }
    } elseif ($KeepArtifacts) {
        Write-Host "QA artifacts kept at $runDir"
    }
}
