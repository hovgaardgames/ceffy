[CmdletBinding()]
param(
    [switch]$Fix,
    [switch]$Tidy
)

$ErrorActionPreference = "Stop"
$nativeDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $nativeDir "src"), (Join-Path $nativeDir "helper") -File |
    Where-Object { $_.Extension -in ".cpp", ".h" }

function FindClangTool($name) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswherePath) {
        $path = & $vswherePath -latest -products * -find "VC\Tools\Llvm\bin\$name.exe" |
            Select-Object -First 1
        if ($path) {
            return $path
        }
    }

    throw "$name was not found. Install the Visual Studio LLVM tools."
}

$clangFormatPath = FindClangTool "clang-format"

$formatFailures = 0
foreach ($file in $sourceFiles) {
    if ($Fix) {
        & $clangFormatPath --style=file -i $file.FullName
    } else {
        & $clangFormatPath --style=file --dry-run --Werror $file.FullName
    }

    if ($LASTEXITCODE -ne 0) {
        $formatFailures++
    }
}

if ($formatFailures -gt 0) {
    throw "clang-format failed for $formatFailures file(s)."
}

Write-Output "clang-format passed for $($sourceFiles.Count) files."

if (-not $Tidy) {
    return
}

$databaseDir = Join-Path $nativeDir "cmake-build-debug"
if (-not (Test-Path -LiteralPath (Join-Path $databaseDir "compile_commands.json"))) {
    & cmake -S $nativeDir -B $databaseDir -G Ninja -DCMAKE_EXPORT_COMPILE_COMMANDS=ON
    if ($LASTEXITCODE -ne 0) {
        throw "CMake could not create the clang-tidy compilation database."
    }
}

$clangTidyPath = FindClangTool "clang-tidy"

foreach ($file in $sourceFiles) {
    if ($file.Extension -ne ".cpp") {
        continue
    }

    & $clangTidyPath -p $databaseDir $file.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "clang-tidy failed for $($file.FullName)."
    }
}

Write-Output "clang-tidy passed."
