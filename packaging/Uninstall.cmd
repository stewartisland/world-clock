@echo off
rem Removes World Clock. You can also uninstall it from Settings ^> Apps.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%LOCALAPPDATA%\Programs\World Clock\Uninstall.ps1"
