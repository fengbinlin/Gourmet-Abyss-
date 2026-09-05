param(
    [string]$UnityPath
)

$ErrorActionPreference = "Stop"
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectVersionFile = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$resultDirectory = Join-Path $projectPath "Library\CameraAcceptanceResults"
$resultPath = Join-Path $resultDirectory "ProductionScenes-results.xml"
$logPath = Join-Path $resultDirectory "ProductionScenes.log"
$reportPath = Join-Path $resultDirectory "Camera-Acceptance-Report.md"
New-Item -ItemType Directory -Force $resultDirectory | Out-Null

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $versionLine = Get-Content -LiteralPath $projectVersionFile |
        Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
        Select-Object -First 1
    if ($versionLine -match '^m_EditorVersion:\s*(.+)$') {
        $editorVersion = $Matches[1].Trim()
        $UnityPath = Join-Path "C:\Program Files\Unity\Hub\Editor" "$editorVersion\Editor\Unity.exe"
    }
}

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable not found for the project version. Pass -UnityPath explicitly. Resolved path: $UnityPath"
}

function Assert-UnityProjectAvailable {
    $lockPath = Join-Path $projectPath "Temp\UnityLockfile"
    if (-not (Test-Path -LiteralPath $lockPath)) { return }

    try {
        $lockStream = [System.IO.File]::Open(
            $lockPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        $lockStream.Dispose()
    }
    catch {
        throw "This Unity project is already open. Save and close the Unity Editor, then run this script again."
    }
}

Assert-UnityProjectAvailable

Write-Host "[1/2] Running camera framework regression tests..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "Test-CameraFramework.ps1") -UnityPath $UnityPath

if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath -Force }
if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }

Write-Host "[2/2] Loading production scenes and running camera requirements acceptance..." -ForegroundColor Cyan
$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", "`"$projectPath`"",
    "-runTests",
    "-testPlatform", "PlayMode",
    "-testFilter", "GourmetAbyss.CameraSystem.Acceptance",
    "-testResults", "`"$resultPath`"",
    "-logFile", "`"$logPath`""
)
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(1200000)) {
    Stop-Process -Id $process.Id -Force
    throw "Production-scene acceptance timed out after 20 minutes. See $logPath"
}

$unityExitCode = $process.ExitCode
if (-not (Test-Path -LiteralPath $resultPath)) {
    throw "Production-scene acceptance did not produce results. ExitCode=$unityExitCode. See $logPath"
}

[xml]$acceptance = Get-Content -LiteralPath $resultPath
[xml]$editRegression = Get-Content -LiteralPath (Join-Path $projectPath "Library\CameraFrameworkTestResults\EditMode-results.xml")
[xml]$playRegression = Get-Content -LiteralPath (Join-Path $projectPath "Library\CameraFrameworkTestResults\PlayMode-results.xml")
$testCases = @($acceptance.SelectNodes('//test-case'))
$nameMap = @{
    "TownScene_ActuallyFollowsFacingDirectionWithSmoothLookAhead" = "Town: production player facing look-ahead and smooth follow"
    "DungeonScene_ActuallyUsesDeadZoneUiBlockingAndCappedMouseOffset" = "Dungeon: pointer dead-zone, UI blocking, and capped offset"
    "RestaurantInTown_ActuallyLocksCentersPansBoundsAndRestores" = "Restaurant: lock, center, bounded pan, and restore"
    "ProductionLevelManagerPipeline_ActuallyRebindsTownDungeonTownWithoutLeaks" = "Transition: map UI and LevelManager run Town -> Dungeon -> Town without leaks"
}

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Gourmet Abyss Camera Requirements Acceptance Report")
$report.Add("")
$report.Add("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$report.Add("- Unity: $([System.IO.Path]::GetFileName([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetDirectoryName($UnityPath))))")
$report.Add("- Production scenes: UpGround, Layer1")
$report.Add("- Framework EditMode: $($editRegression.'test-run'.passed)/$($editRegression.'test-run'.total) passed")
$report.Add("- Framework PlayMode: $($playRegression.'test-run'.passed)/$($playRegression.'test-run'.total) passed")
$report.Add("- Production-scene acceptance: $($acceptance.'test-run'.passed)/$($acceptance.'test-run'.total) passed")
$report.Add("")
$report.Add("| Requirement | Result | Duration (seconds) |")
$report.Add("|---|---:|---:|")
foreach ($testCase in $testCases) {
    $shortName = [string]$testCase.name
    $displayName = if ($nameMap.ContainsKey($shortName)) { $nameMap[$shortName] } else { $shortName }
    $result = [string]$testCase.result
    $duration = [math]::Round([double]$testCase.duration, 2)
    $report.Add("| $displayName | $result | $duration |")
}
$report.Add("")
$report.Add("## Acceptance criteria")
$report.Add("")
$report.Add("- The Town camera follows the production player smoothly and applies bounded facing look-ahead.")
$report.Add("- The Layer1 Dungeon camera is angled and orthographic; pointer offset has a dead-zone, UI blocking, and a 3-meter cap.")
$report.Add("- The production Restaurant E entry moves the player to a locked seat, centers the shot, bounds middle-button panning, and restores the door pose on exit.")
$report.Add("- The production map UI and LevelManager pipeline loads Layer1 additively; the dungeon exit returns to the original UpGround player and camera without leaking requests.")
$report.Add("- Tilted-camera geometry and CameraFacingVisual are covered by framework tests. UpGround keeps its legacy flat layered-art presentation because forcing a physical tilt deforms the current authored assets; final town tilt remains an art-content migration and visual-signoff item.")
$report | Set-Content -LiteralPath $reportPath -Encoding UTF8

$failed = [int]$acceptance.'test-run'.failed
$passed = [int]$acceptance.'test-run'.passed
if ($unityExitCode -ne 0 -or $failed -ne 0) {
    Write-Host "Acceptance report: $reportPath" -ForegroundColor Yellow
    throw "Camera requirements acceptance failed. Passed=$passed Failed=$failed ExitCode=$unityExitCode. See $logPath"
}

Write-Host "Production scenes passed: $passed" -ForegroundColor Green
Write-Host "Acceptance report: $reportPath" -ForegroundColor Green
Write-Host "All camera requirements acceptance checks passed." -ForegroundColor Green
