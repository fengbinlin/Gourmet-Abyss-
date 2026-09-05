@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-CameraFramework.ps1"
if errorlevel 1 (
  echo.
  echo Camera Framework tests FAILED.
  pause
  exit /b 1
)
echo.
echo Camera Framework tests PASSED.
pause
