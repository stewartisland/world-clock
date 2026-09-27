@echo off
rem Installs World Clock for the current user (no admin rights needed).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
pause
