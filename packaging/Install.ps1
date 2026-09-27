# Installs World Clock for the current user. No administrator rights needed.
#   - copies WorldClock.exe to %LOCALAPPDATA%\Programs\World Clock
#   - adds a Start menu shortcut (and, if you choose, a desktop shortcut)
#   - registers it in Settings > Apps so it can be uninstalled from there
param([switch]$Quiet)

$ErrorActionPreference = "Stop"
$source = $PSScriptRoot
$dest = Join-Path $env:LOCALAPPDATA "Programs\World Clock"
$exe = Join-Path $dest "WorldClock.exe"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\World Clock.lnk"
$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "World Clock.lnk"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WorldClock"
$version = (Get-Item (Join-Path $source "WorldClock.exe")).VersionInfo.ProductVersion.Split("+")[0]

Write-Host "Installing World Clock $version..."

# Close a running copy of the installed app so it can be replaced.
Get-Process WorldClock -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object {
    $_.CloseMainWindow() | Out-Null
    if (-not $_.WaitForExit(5000)) { $_.Kill() }
}

New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $source "WorldClock.exe") $exe -Force
Copy-Item (Join-Path $source "Uninstall.ps1") (Join-Path $dest "Uninstall.ps1") -Force
# Files from a downloaded zip are marked as coming from the internet; clear that so Windows doesn't block them.
Get-ChildItem $dest | Unblock-File

function New-Shortcut($path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $dest
    $link.Description = "The time and weather in the places you care about"
    $link.Save()
}

New-Shortcut $startMenu
Write-Host "  Added World Clock to the Start menu."

if (-not $Quiet) {
    $answer = Read-Host "  Add a desktop shortcut too? (Y/N)"
    if ($answer -match "^[Yy]") {
        New-Shortcut $desktop
        Write-Host "  Added a desktop shortcut."
    }
}

New-Item -Force $uninstallKey | Out-Null
$uninstall = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$dest\Uninstall.ps1`""
Set-ItemProperty $uninstallKey -Name DisplayName -Value "World Clock"
Set-ItemProperty $uninstallKey -Name DisplayVersion -Value $version
Set-ItemProperty $uninstallKey -Name Publisher -Value "Brendon"
Set-ItemProperty $uninstallKey -Name InstallLocation -Value $dest
Set-ItemProperty $uninstallKey -Name DisplayIcon -Value $exe
Set-ItemProperty $uninstallKey -Name UninstallString -Value $uninstall
Set-ItemProperty $uninstallKey -Name URLInfoAbout -Value "https://github.com/stewartisland/world-clock"
Set-ItemProperty $uninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty $uninstallKey -Name NoRepair -Value 1 -Type DWord
Set-ItemProperty $uninstallKey -Name EstimatedSize -Value ([int]((Get-Item $exe).Length / 1KB)) -Type DWord

Write-Host "Done. World Clock is installed in $dest"

if (-not $Quiet) {
    Start-Process $exe
}
