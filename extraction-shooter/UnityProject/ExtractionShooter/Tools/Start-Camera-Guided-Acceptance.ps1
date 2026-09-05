param(
    [string]$ServerUrl = "http://127.0.0.1:8080"
)

$ErrorActionPreference = "Stop"
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectName = Split-Path $projectPath -Leaf

try {
    $instanceResponse = Invoke-RestMethod -Uri "$ServerUrl/api/instances" -Method Get -TimeoutSec 10
}
catch {
    throw "Unity MCP is not running. Open Unity, press Ctrl+Shift+M, then Start Server and Connect."
}

$instances = @($instanceResponse.instances)
if ($instances.Count -eq 0) {
    throw "No Unity Editor is connected. In Unity press Ctrl+Shift+M and click Connect."
}

$matching = @($instances | Where-Object { $_.project -eq $projectName })
if ($matching.Count -eq 1) {
    $selected = $matching[0]
}
elseif ($instances.Count -eq 1) {
    $selected = $instances[0]
}
else {
    throw "Multiple Unity Editors are connected. Close unrelated Editors and retry."
}

$instanceId = "$($selected.project)@$($selected.hash)"
$payload = @{
    type = "execute_menu_item"
    params = @{
        menu_path = "Tools/Gourmet Abyss/Camera/Start Guided Acceptance"
    }
    unity_instance = $instanceId
} | ConvertTo-Json -Depth 8

$response = Invoke-RestMethod `
    -Uri "$ServerUrl/api/command" `
    -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body ([System.Text.Encoding]::UTF8.GetBytes($payload)) `
    -TimeoutSec 45

if ($response.status -ne "success" -or -not $response.result.success) {
    $detail = if ($response.result.error) { $response.result.error } else { $response | ConvertTo-Json -Depth 8 }
    throw "Could not start guided acceptance: $detail"
}

Write-Host "Guided camera acceptance started in $instanceId." -ForegroundColor Green
Write-Host "Switch to the Unity Game window." -ForegroundColor Cyan
Write-Host "F9 = next step/location, F8 = hide/show panel, F10 = restart guide." -ForegroundColor Yellow
