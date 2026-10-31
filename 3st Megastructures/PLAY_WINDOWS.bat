@echo off
rem Thread - double-click this file to start the game on Windows.
if exist "%~dp0Game\Windows\Thread.exe" (
  start "" "%~dp0Game\Windows\Thread.exe" %*
) else if exist "%~dp0Builds\Thread\Thread.exe" (
  start "" "%~dp0Builds\Thread\Thread.exe" %*
) else (
  echo Thread.exe not found. Download the whole folder, including Game\Windows.
  pause
)
