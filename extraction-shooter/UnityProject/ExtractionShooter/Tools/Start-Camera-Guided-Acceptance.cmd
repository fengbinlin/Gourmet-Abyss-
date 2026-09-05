@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Camera-Guided-Acceptance.ps1"
if errorlevel 1 (
  echo.
  echo FAILED TO START GUIDED CAMERA ACCEPTANCE.
  pause
  exit /b 1
)
echo.
echo Switch to Unity Game view. Press N/F9 or click the panel button for each next step.
pause
