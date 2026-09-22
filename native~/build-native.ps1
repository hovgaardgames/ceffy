<#
.SYNOPSIS
    Builds the Ceffy native plugin and stages it to NativeRuntime/win-x64.

.DESCRIPTION
    This script will:
      1) Configure CMake (if needed)
      2) Build ceffy_native + CeffyHelper
      3) Copy build output to NativeRuntime/win-x64

    Use this instead of manually running cmake + copy commands.
#>

[CmdletBinding()]
param(
    [ValidateSet("Release", "RelWithDebInfo", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64")]
    [string]$Rid = "win-x64",

    [switch]$SkipConfigure
)

$ErrorActionPreference = "Stop"

$nativeDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $nativeDir

$cefDir      = Join-Path $nativeDir "cef"
$buildDir    = Join-Path $nativeDir "build"
$releaseDir  = Join-Path $buildDir $Configuration
$runtimeDest = Join-Path $projectRoot (Join-Path "NativeRuntime" $Rid)

if (-not (Test-Path $cefDir)) {
    Write-Host "CEF SDK not found. Running setup..."
    & (Join-Path $nativeDir "setup.ps1")

    if (-not (Test-Path $cefDir)) {
        throw "CEF setup did not create the expected SDK directory: $cefDir"
    }
}

if (-not $SkipConfigure) {
    Write-Host "Configuring CMake..."
    $cefDirForCmake = $cefDir -replace '\\', '/'
    cmake -S $nativeDir -B $buildDir -G "Visual Studio 17 2022" -A x64 -DCEF_ROOT="$cefDirForCmake"
    if ($LASTEXITCODE -ne 0) {
        throw "CMake configuration failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Building ($Configuration)..."
cmake --build $buildDir --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $releaseDir)) {
    throw "Build output folder not found: $releaseDir"
}

Write-Host "Staging runtime to: $runtimeDest"
if (-not (Test-Path $runtimeDest)) {
    New-Item -ItemType Directory -Path $runtimeDest | Out-Null
}

$failedCopies = @()

Get-ChildItem -Path $releaseDir -Recurse -Directory | ForEach-Object {
    $relative = $_.FullName.Substring($releaseDir.Length).TrimStart('\','/')
    $destDir = Join-Path $runtimeDest $relative
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir | Out-Null
    }
}

Get-ChildItem -Path $releaseDir -Recurse -File |
    Where-Object { $_.Extension -notin @(".exp", ".lib") } |
    ForEach-Object {
    $relative = $_.FullName.Substring($releaseDir.Length).TrimStart('\','/')
    $destFile = Join-Path $runtimeDest $relative
    try {
        Copy-Item -Force $_.FullName $destFile -ErrorAction Stop
    }
    catch {
        $failedCopies += $destFile
    }
}

Write-Host ""
Write-Host "=== Done ==="
Write-Host "Built:  $releaseDir"
Write-Host "Staged: $runtimeDest"
if ($failedCopies.Count -gt 0) {
    Write-Warning "$($failedCopies.Count) file(s) could not be updated (likely locked by Unity/CEF)."
    $failedCopies | ForEach-Object { Write-Warning "  Locked: $_" }
}
Write-Host ""
