# Removes World Clock for the current user. Your clocks are kept unless you choose to remove them.
param([switch]$Quiet)

$ErrorActionPreference = "Stop"
$dest = Join-Path $env:LOCALAPPDATA "Programs\World Clock"
$exe = Join-Path $dest "WorldClock.exe"
$settings = Join-Path $env:APPDATA "WorldClock"

Write-Host "Uninstalling World Clock..."

Get-Process WorldClock -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object {
    $_.CloseMainWindow() | Out-Null
    if (-not $_.WaitForExit(5000)) { $_.Kill() }
}

Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\World Clock.lnk") -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath("Desktop")) "World Clock.lnk") -ErrorAction SilentlyContinue
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WorldClock" -Recurse -ErrorAction SilentlyContinue
Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue

if (-not $Quiet -and (Test-Path $settings)) {
    $answer = Read-Host "  Also delete your saved clocks and settings ($settings)? (Y/N)"
    if ($answer -match "^[Yy]") {
        Remove-Item $settings -Recurse -Force
        Write-Host "  Deleted your saved clocks."
    } else {
        Write-Host "  Kept your saved clocks. They'll be there if you install World Clock again."
    }
}

Write-Host "Done. World Clock has been removed."
if (-not $Quiet) { Read-Host "Press Enter to close" | Out-Null }
