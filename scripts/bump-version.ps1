<#
.SYNOPSIS
    Bumps the World Clock version (Major.Minor) and records it in CHANGELOG.md.

.DESCRIPTION
    The version lives only in <Version> in WorldClock.csproj; the app reads it at run time.
      minor  new features, improvements and fixes   1.2 -> 1.3
      major  big or breaking changes                1.2 -> 2.0

.EXAMPLE
    ./scripts/bump-version.ps1 minor -Notes "Added rain radar", "Fixed Dallas weather"
    powershell -File scripts/bump-version.ps1 minor -Notes "Added rain radar; Fixed Dallas weather"

.EXAMPLE
    ./scripts/bump-version.ps1 major -Notes "New settings file format" -Commit
    Also commits the change and creates an annotated git tag (v2.0). Push with: git push --follow-tags
#>
param(
    [Parameter(Mandatory)][ValidateSet("major", "minor")][string]$Part,
    [Parameter(Mandatory)][string[]]$Notes,
    [switch]$Commit
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $root "WorldClock.csproj"
$changelog = Join-Path $root "CHANGELOG.md"

$project = [IO.File]::ReadAllText($csproj)
$match = [regex]::Match($project, "<Version>(\d+)\.(\d+)</Version>")
if (-not $match.Success) { throw "Couldn't find <Version>Major.Minor</Version> in WorldClock.csproj" }

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$old = "$major.$minor"
if ($Part -eq "major") { $major++; $minor = 0 } else { $minor++ }
$new = "$major.$minor"

[IO.File]::WriteAllText($csproj, $project.Replace($match.Value, "<Version>$new</Version>"))

# Insert the new entry above the most recent one.
$date = (Get-Date).ToString("yyyy-MM-dd")
# Notes can be a list ("A", "B") or one string split with ";" (needed when run via powershell -File).
$lines = $Notes | ForEach-Object { $_ -split ";" } | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$entry = "## $new ($date)`n`n" + (($lines | ForEach-Object { "- $_" }) -join "`n") + "`n`n"
$log = [IO.File]::ReadAllText($changelog)
$first = $log.IndexOf("`n## ")
if ($first -lt 0) { $log = $log.TrimEnd() + "`n`n" + $entry } else { $log = $log.Insert($first + 1, $entry) }
[IO.File]::WriteAllText($changelog, $log)

Write-Host "Version $old -> $new"

if ($Commit) {
    git -C $root add WorldClock.csproj CHANGELOG.md
    git -C $root commit -m "Release $new" | Out-Null
    git -C $root tag -a "v$new" -m "World Clock $new"
    Write-Host "Committed and tagged v$new. Push with: git push --follow-tags"
}
