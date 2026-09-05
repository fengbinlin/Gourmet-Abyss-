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
echo Switch to Unity Game view. Press F9 for each next step.
pause
