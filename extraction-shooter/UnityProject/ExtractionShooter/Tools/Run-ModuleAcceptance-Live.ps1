param([string]$ServerUrl="http://127.0.0.1:8080")
$ErrorActionPreference="Stop"
$moduleProject=Split-Path (Split-Path $PSScriptRoot -Parent) -Leaf
$moduleInstances=Invoke-RestMethod "$ServerUrl/api/instances" -TimeoutSec 10
$moduleMatch=@($moduleInstances.instances | Where-Object {$_.project -eq $moduleProject})
if($moduleMatch.Count -ne 1){throw "Expected one connected Unity instance for $moduleProject."}
$moduleInstance="$($moduleMatch[0].project)@$($moduleMatch[0].hash)"
function Invoke-ModuleCode([string]$Code){
    $moduleBody=@{type="execute_code";unity_instance=$moduleInstance;params=@{action="execute";code=$Code}} | ConvertTo-Json -Depth 6
    $moduleResponse=Invoke-WebRequest "$ServerUrl/api/command" -UseBasicParsing -Method Post -ContentType "application/json" -Body $moduleBody -TimeoutSec 40
    $moduleResult=[System.Text.Encoding]::UTF8.GetString($moduleResponse.RawContentStream.ToArray()) | ConvertFrom-Json
    if(!$moduleResult.result.success){throw ($moduleResult | ConvertTo-Json -Depth 8)}
    return $moduleResult.result.data.result
}
Write-Host "Unity must be playing UpGround, outside the restaurant. Watch the Game view."
Invoke-ModuleCode 'Game.Modules.Editor.ModuleAcceptanceWindow.ShowWindow(); Game.Modules.Editor.ModuleAcceptanceWindow.Begin(true); return "Started";'
$moduleDeadline=(Get-Date).AddMinutes(2)
do {
    Start-Sleep -Seconds 3
    $moduleStatus=Invoke-ModuleCode 'return Game.Modules.Editor.ModuleAcceptanceWindow.Status;'
    Write-Host $moduleStatus
    if($moduleStatus -eq "PASS"){exit 0}
    if($moduleStatus -notlike "Running*"){throw $moduleStatus}
} while((Get-Date) -lt $moduleDeadline)
throw "Timed out. Stop the acceptance from Tools/Modules/Restaurant Acceptance."
