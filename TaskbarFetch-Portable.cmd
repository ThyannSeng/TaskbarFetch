@echo off
title TaskbarFetch Portable Launcher
cd /d "%~dp0"

if exist "%~dp0TaskbarFetch.exe" (
  start "" "%~dp0TaskbarFetch.exe"
  exit /b 0
)

if not exist "%~dp0TaskbarFetch-Build.cmd" (
  echo ERROR: TaskbarFetch.exe and TaskbarFetch-Build.cmd were not found.
  pause
  exit /b 1
)

call "%~dp0TaskbarFetch-Build.cmd"
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

start "" "%~dp0dist\TaskbarFetch.exe"
