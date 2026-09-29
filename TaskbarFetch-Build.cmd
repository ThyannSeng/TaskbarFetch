@echo off
title TaskbarFetch Build
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo ERROR: Windows .NET Framework compiler csc.exe was not found.
  echo Enable/install .NET Framework 4.8, then run TaskbarFetch-Build.cmd again.
  exit /b 1
)

if not exist "dist" mkdir "dist"

echo Building TaskbarFetch...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /win32manifest:"app.manifest" ^
  /win32icon:"assets\TaskbarFetch.ico" ^
  /out:"dist\TaskbarFetch.exe" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:Accessibility.dll ^
  "src\TaskbarFetch.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED.
  exit /b 1
)

echo.
echo Build succeeded: "%CD%\dist\TaskbarFetch.exe"
exit /b 0
