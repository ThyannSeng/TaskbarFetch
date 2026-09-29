@echo off
title TaskbarFetch Portable Launcher
cd /d "%~dp0"

set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%POWERSHELL%" set "POWERSHELL=powershell.exe"

"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\TaskbarFetch-Portable-Launch.ps1"
exit /b %ERRORLEVEL%
