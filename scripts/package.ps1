<#
.SYNOPSIS
    Builds an installable zip of the Windows app: dist/WorldClock-<version>-win-x64.zip

.DESCRIPTION
    Checks out a release tag (default: the latest v* tag) into a temporary git worktree, so your working copy
    and current branch aren't touched. Then it publishes a self-contained single-file WorldClock.exe (the other
    PC doesn't need .NET) and zips it with the installer files from packaging/.

    Run on Windows (WPF only builds there).

.EXAMPLE
    powershell -File scripts/package.ps1            # latest release tag
    powershell -File scripts/package.ps1 -Tag v1.4  # a specific release
#>
param([string]$Tag)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

if (-not $Tag) {
    $Tag = git -C $root tag --list "v*" --sort=-version:refname | Select-Object -First 1
    if (-not $Tag) { throw "No v* release tags found. Pass -Tag." }
}
git -C $root rev-parse --verify --quiet "$Tag^{commit}" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Tag $Tag doesn't exist." }

$version = $Tag.TrimStart("v")
$name = "WorldClock-$version"
$work = Join-Path ([IO.Path]::GetTempPath()) "worldclock-package-$([guid]::NewGuid().ToString('N'))"
$stage = Join-Path $work "stage\$name"
$dist = Join-Path $root "dist"
$zip = Join-Path $dist "$name-win-x64.zip"

Write-Host "Packaging World Clock $version from $Tag..."
git -C $root worktree add --detach --quiet (Join-Path $work "src") $Tag
if ($LASTEXITCODE -ne 0) { throw "Couldn't check out $Tag." }

try {
    dotnet publish (Join-Path $work "src\WorldClock.csproj") -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $stage -nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    # The installer files come from this working copy, so installer fixes apply to older releases too.
    foreach ($file in "Install.cmd", "Install.ps1", "Uninstall.cmd", "Uninstall.ps1") {
        Copy-Item (Join-Path $root "packaging\$file") $stage
    }
    $readme = [IO.File]::ReadAllText((Join-Path $root "packaging\README.txt")).Replace("{VERSION}", $version)
    [IO.File]::WriteAllText((Join-Path $stage "README.txt"), $readme)

    New-Item -ItemType Directory -Force $dist | Out-Null
    Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal -Force
    Write-Host ("Created {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}
finally {
    git -C $root worktree remove --force (Join-Path $work "src") 2>$null
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
