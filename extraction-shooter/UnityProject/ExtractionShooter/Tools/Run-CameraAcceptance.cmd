@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-CameraAcceptance.ps1"
if errorlevel 1 (
  echo.
  echo CAMERA REQUIREMENTS ACCEPTANCE FAILED.
  pause
  exit /b 1
)
echo.
echo CAMERA REQUIREMENTS ACCEPTANCE PASSED.
pause
