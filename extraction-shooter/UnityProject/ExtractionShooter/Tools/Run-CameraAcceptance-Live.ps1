param(
    [string]$ServerUrl = "http://127.0.0.1:8080",
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectName = Split-Path $projectPath -Leaf
$resultDirectory = Join-Path $projectPath "Library\CameraAcceptanceResults"
$reportPath = Join-Path $resultDirectory "Camera-Live-Acceptance-Report.md"
$rawPath = Join-Path $resultDirectory "Camera-Live-Acceptance-Results.json"
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null

function Invoke-UnityMcpCommand {
    param(
        [string]$Type,
        [hashtable]$Params,
        [string]$Instance
    )

    $payload = @{
        type = $Type
        params = $Params
        unity_instance = $Instance
    } | ConvertTo-Json -Depth 12

    $response = Invoke-RestMethod `
        -Uri "$ServerUrl/api/command" `
        -Method Post `
        -ContentType "application/json" `
        -Body $payload `
        -TimeoutSec 45

    if ($response.status -ne "success" -or -not $response.result.success) {
        $detail = if ($response.result.error) { $response.result.error } else { $response | ConvertTo-Json -Depth 8 }
        throw "Unity MCP command '$Type' failed: $detail"
    }

    return $response.result
}

function Invoke-LiveTestSuite {
    param(
        [string]$Label,
        [string]$Mode,
        [string]$GroupName,
        [string]$Instance
    )

    Write-Host "Starting $Label..." -ForegroundColor Cyan
    $start = Invoke-UnityMcpCommand `
        -Type "run_tests" `
        -Instance $Instance `
        -Params @{
            mode = $Mode
            groupNames = @($GroupName)
            includeDetails = $true
            includeFailedTests = $true
            initTimeout = 120000
        }

    $jobId = $start.data.job_id
    if ([string]::IsNullOrWhiteSpace($jobId)) {
        throw "$Label did not return a test job id."
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastProgress = ""
    do {
        Start-Sleep -Seconds 1
        $poll = Invoke-UnityMcpCommand `
            -Type "get_test_job" `
            -Instance $Instance `
            -Params @{
                job_id = $jobId
                includeDetails = $true
                includeFailedTests = $true
            }

        $status = [string]$poll.data.status
        $progress = $poll.data.progress
        if ($progress) {
            $progressText = "$($progress.completed)/$($progress.total) $($progress.current_test_full_name)"
            if ($progressText -ne $lastProgress) {
                Write-Host "  $progressText"
                $lastProgress = $progressText
            }
        }

        if ($status -in @("succeeded", "failed", "cancelled")) {
            break
        }
    } while ((Get-Date) -lt $deadline)

    if ($status -notin @("succeeded", "failed", "cancelled")) {
        throw "$Label timed out after $TimeoutSeconds seconds. JobId=$jobId"
    }

    $summary = $poll.data.result.summary
    if ($summary) {
        $color = if ([int]$summary.failed -eq 0 -and $status -eq "succeeded") { "Green" } else { "Red" }
        Write-Host "${Label}: $($summary.passed)/$($summary.total) passed" -ForegroundColor $color
    }
    else {
        Write-Host "${Label}: $status (no summary returned)" -ForegroundColor Red
    }

    return [pscustomobject]@{
        label = $Label
        mode = $Mode
        group = $GroupName
        status = $status
        data = $poll.data
    }
}

try {
    $instanceResponse = Invoke-RestMethod -Uri "$ServerUrl/api/instances" -Method Get -TimeoutSec 10
}
catch {
    throw "Unity MCP is not running at $ServerUrl. In Unity press Ctrl+Shift+M, choose Connect, then click Start Server and Connect."
}

$instances = @($instanceResponse.instances)
if ($instances.Count -eq 0) {
    throw "Unity MCP is running, but no Unity Editor is connected. In Unity press Ctrl+Shift+M and click Connect."
}

$matchingInstances = @($instances | Where-Object { $_.project -eq $projectName })
if ($matchingInstances.Count -eq 1) {
    $selected = $matchingInstances[0]
}
elseif ($instances.Count -eq 1) {
    $selected = $instances[0]
}
else {
    throw "Multiple Unity Editors are connected and none uniquely matches '$projectName'. Close unrelated Editors and retry."
}

$instanceId = "$($selected.project)@$($selected.hash)"
Write-Host "Connected: $instanceId (Unity $($selected.unity_version))" -ForegroundColor Green
Write-Host "Keep the Unity window visible to watch production scenes load and PlayMode run." -ForegroundColor Yellow

$suites = @(
    [pscustomobject]@{ Label = "Framework EditMode"; Mode = "EditMode"; Group = "GourmetAbyss.CameraSystem.Tests" },
    [pscustomobject]@{ Label = "Framework PlayMode"; Mode = "PlayMode"; Group = "GourmetAbyss.CameraSystem.Tests" },
    [pscustomobject]@{ Label = "Production Scenes"; Mode = "PlayMode"; Group = "GourmetAbyss.CameraSystem.Acceptance" }
)

$results = New-Object System.Collections.Generic.List[object]
foreach ($suite in $suites) {
    $results.Add((Invoke-LiveTestSuite `
        -Label $suite.Label `
        -Mode $suite.Mode `
        -GroupName $suite.Group `
        -Instance $instanceId))
}

$results | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $rawPath -Encoding UTF8

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Gourmet Abyss Camera Live Acceptance Report")
$report.Add("")
$report.Add("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$report.Add("- Unity: $($selected.unity_version)")
$report.Add("- Instance: $instanceId")
$report.Add("- Production scenes: UpGround, Layer1")
$report.Add("")
$report.Add("| Suite | Mode | Passed | Failed | Skipped | Duration (seconds) |")
$report.Add("|---|---:|---:|---:|---:|---:|")

$allPassed = $true
foreach ($result in $results) {
    $summary = $result.data.result.summary
    if (-not $summary) {
        $allPassed = $false
        $report.Add("| $($result.label) | $($result.mode) | 0 | 1 | 0 | 0 |")
        continue
    }

    if ([int]$summary.failed -ne 0 -or $result.status -ne "succeeded") {
        $allPassed = $false
    }
    $duration = [math]::Round([double]$summary.durationSeconds, 2)
    $report.Add("| $($result.label) | $($result.mode) | $($summary.passed) | $($summary.failed) | $($summary.skipped) | $duration |")
}

$report.Add("")
$report.Add("Result: $(if ($allPassed) { 'PASS' } else { 'FAIL' })")
$report.Add("")
$report.Add("## Individual checks")
$report.Add("")
$report.Add("| Suite | Check | Result | Duration (seconds) |")
$report.Add("|---|---|---:|---:|")
$testNameMap = @{
    "GameplayCameraLensHasSingleWriter" = "Single gameplay camera writer"
    "NoGameplayCodeUsesLegacyStaticFocusRequests" = "No legacy static focus requests"
    "GameplayBuildScenesContainCameraFollowBootstrap" = "Every gameplay build scene has a camera bootstrap"
    "CameraPlane_FrontCameraUsesXYPlane" = "Legacy flat XY plane compatibility"
    "CameraPlane_TiltedCameraUsesXZGround" = "Tilted ground-plane geometry"
    "BoundsConstraint_ClampsFrontOrthographicCamera" = "Orthographic bounds clamping"
    "BoundsConstraint_CentersWhenViewIsLargerThanBounds" = "Small content remains centered"
    "CameraFacingVisual_AlignsVisualPlaneToTiltedCamera" = "2D visual faces a tilted camera"
    "DungeonPointerOffsetIsCapped" = "Dungeon pointer cap"
    "RestaurantPanMovesWhenContentExceedsViewport_AndRemainsBounded" = "Oversized restaurant can pan but stays bounded"
    "TownScene_ActuallyFollowsFacingDirectionWithSmoothLookAhead" = "Production Town smooth follow and facing look-ahead"
    "DungeonScene_ActuallyUsesDeadZoneUiBlockingAndCappedMouseOffset" = "Production Dungeon dead-zone, UI block, response, cap, and smooth follow"
    "RestaurantInTown_ActuallyLocksCentersPansBoundsAndRestores" = "Production Restaurant E entry, seat lock, center, pan, and door restore"
    "ProductionLevelManagerPipeline_ActuallyRebindsTownDungeonTownWithoutLeaks" = "Production map UI and LevelManager Town -> Dungeon -> Town pipeline"
}
foreach ($suiteResult in $results) {
    $checks = @($suiteResult.data.result.results)
    foreach ($check in $checks) {
        $checkName = [string]$check.name
        $displayName = if ($testNameMap.ContainsKey($checkName)) { $testNameMap[$checkName] } else { $checkName }
        $duration = [math]::Round([double]$check.durationSeconds, 2)
        $report.Add("| $($suiteResult.label) | $displayName | $($check.state) | $duration |")
    }
}
$report.Add("")
$report.Add("## Visual sign-off scope")
$report.Add("")
$report.Add("- Layer1 uses the production orthographic tilted camera, and the framework verifies that 2D visual roots can face any tilted camera without rotating physics roots.")
$report.Add("- UpGround keeps its existing flat layered-art presentation. A forced physical tilt visibly deforms that legacy composition, so a non-zero Town tilt requires art-content reauthoring and visual sign-off; it is intentionally not reported as an automated pass.")
$report | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Raw results: $rawPath"
Write-Host "Report: $reportPath"
if (-not $allPassed) {
    throw "Camera live acceptance failed. See $reportPath"
}

Write-Host "All live camera acceptance checks passed." -ForegroundColor Green
