<#
.SYNOPSIS
    Downloads and extracts the CEF binary distribution, then generates
    the Visual Studio solution via CMake.

.DESCRIPTION
    Run this once before building. It will:
      1. Download the CEF binary distribution (Minimal) for Windows x64
      2. Extract it into native~/cef/
      3. Configure CMake to generate a VS solution in native~/build/

.PARAMETER CefVersion
    The CEF version string from cef-builds.spotifycdn.com.
    The default is declared in the param block below.
#>


param(
    [string]$CefVersion = "152.0.6+g708dc14+chromium-152.0.7977.83",
    [string]$CefSha1 = "E5E3020627F4528BD43E22F4C4970000B0458E99"
)

function Validate {

    $cmake = Get-Command cmake -ErrorAction SilentlyContinue
    if ($null -eq $cmake) {
        Write-Error "CMake was not found. Install CMake and ensure it is available on PATH."
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    $visualStudioPath = if (Test-Path $vswherePath) {
        & $vswherePath -latest -products * -version '[17.0,18.0)' `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    }

    if ([string]::IsNullOrWhiteSpace($visualStudioPath)) {
        Write-Error "Visual Studio 2022 with Desktop development with C++ was not found."
    }

}

$ErrorActionPreference = "Stop"

Validate

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$cefDir      = Join-Path $scriptDir "cef"
$buildDir    = Join-Path $scriptDir "build"

$cefName      = "cef_binary_${CefVersion}_windows64_minimal"
$archiveFile  = Join-Path $scriptDir "$cefName.tar.bz2"
$downloadName = "$cefName.tar.bz2" -replace '\+', '%2B'
$downloadUrl  = "https://cef-builds.spotifycdn.com/$downloadName"

Write-Host "Using CEF version: $CefVersion"

# --- Step 1: Download and verify ---
if (-not (Test-Path $cefDir)) {
    if (-not (Test-Path $archiveFile)) {
        Write-Host "Downloading CEF binary distribution..."
        Write-Host "  URL: $downloadUrl"
        Invoke-WebRequest -Uri $downloadUrl -OutFile $archiveFile -UseBasicParsing
        Write-Host "  Done."
    }

    $archiveSha1 = (Get-FileHash -LiteralPath $archiveFile -Algorithm SHA1).Hash
    if ($archiveSha1 -ne $CefSha1) {
        Write-Error "CEF archive checksum mismatch. Expected '$CefSha1', found '$archiveSha1'."
    }

    Write-Host "Extracting CEF SDK..."
    # Use 7z if available, otherwise tar (Windows 10+ has tar built in)
    if (Get-Command 7z -ErrorAction SilentlyContinue) {
        & 7z x $archiveFile -o"$scriptDir" -y | Out-Null
        $innerTar = Join-Path $scriptDir "$cefName.tar"
        if (Test-Path $innerTar) {
            & 7z x $innerTar -o"$scriptDir" -y | Out-Null
            Remove-Item $innerTar -Force
        }
    } else {
        & tar xf $archiveFile -C "$scriptDir"
    }

    $extractedDir = Join-Path $scriptDir $cefName
    if (Test-Path $extractedDir) {
        Move-Item -LiteralPath $extractedDir -Destination $cefDir
    } else {
        Write-Error "Expected extracted directory '$extractedDir' not found."
    }
    Write-Host "  CEF SDK extracted to: $cefDir"
}

# --- Step 2: CMake configure ---
Write-Host ""
Write-Host "Configuring CMake..."
if (-not (Test-Path $buildDir)) {
    New-Item -ItemType Directory -Path $buildDir | Out-Null
}

$cefDirForCmake = $cefDir -replace '\\', '/'

cmake -S $scriptDir -B $buildDir -G "Visual Studio 17 2022" -A x64 `
      -DCEF_ROOT="$cefDirForCmake"
if ($LASTEXITCODE -ne 0) {
    Write-Error "CMake configuration failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "=== Setup complete ==="
Write-Host "  Solution: $buildDir\ceffy_native.sln"
Write-Host "  Build:    cmake --build $buildDir --config Release"
Write-Host ""