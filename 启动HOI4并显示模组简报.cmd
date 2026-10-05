@echo off
setlocal
set "APP=%~dp0Hoi4ModOverlay.exe"
if not exist "%APP%" set "APP=%~dp0dist\Hoi4ModOverlay.exe"
if not exist "%APP%" (
  echo Hoi4ModOverlay.exe not found.
  pause
  exit /b 1
)
start "HOI4 Mod Companion" "%APP%" --launch-game
