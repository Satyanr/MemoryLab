@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-Installer.ps1" %*
set "buildResult=%ERRORLEVEL%"
if not "%buildResult%"=="0" echo Installer build failed. See the error above.
pause
exit /b %buildResult%
