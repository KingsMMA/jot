@echo off
rem Double-click this file to install or update Jot.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
echo.
pause
