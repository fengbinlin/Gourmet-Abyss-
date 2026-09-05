param(
    [string]$UnityPath
)

$ErrorActionPreference = "Stop"
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectVersionFile = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$resultDirectory = Join-Path $projectPath "Library\CameraFrameworkTestResults"
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
    throw "Unity executable not found for the version in ProjectVersion.txt. Pass -UnityPath explicitly. Resolved path: $UnityPath"
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

function Invoke-CameraTests {
    param([string]$Platform)

    $resultPath = Join-Path $resultDirectory "$Platform-results.xml"
    $logPath = Join-Path $resultDirectory "$Platform.log"
    if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath -Force }
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }

    Write-Host "Running Camera Framework $Platform tests..." -ForegroundColor Cyan
    $arguments = @(
        "-batchmode",
        "-nographics",
        "-projectPath", "`"$projectPath`"",
        "-runTests",
        "-testPlatform", $Platform,
        "-testFilter", "GourmetAbyss.CameraSystem.Tests",
        "-testResults", "`"$resultPath`"",
        "-logFile", "`"$logPath`""
    )
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(900000)) {
        Stop-Process -Id $process.Id -Force
        throw "$Platform timed out after 15 minutes. See $logPath"
    }

    $unityExitCode = $process.ExitCode
    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "$Platform did not produce a test result. Unity exit code: $unityExitCode. See $logPath"
    }

    [xml]$result = Get-Content -LiteralPath $resultPath
    $failed = [int]$result.'test-run'.failed
    $passed = [int]$result.'test-run'.passed
    if ($unityExitCode -ne 0 -or $failed -ne 0) {
        throw "$Platform failed. Passed=$passed Failed=$failed ExitCode=$unityExitCode. See $logPath"
    }

    Write-Host "$Platform passed: $passed" -ForegroundColor Green
}

Invoke-CameraTests -Platform "EditMode"
Invoke-CameraTests -Platform "PlayMode"
Write-Host "All Camera Framework tests passed." -ForegroundColor Green
