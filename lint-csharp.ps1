[CmdletBinding()]
param(
    [switch]$Fix
)

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$lintProject = Join-Path $projectDir "lint-csharp.csproj"
$reportDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ceffy-csharp-lint-" + [guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $reportDir | Out-Null

    $arguments = @(
        "format", "whitespace", $lintProject,
        "--report", $reportDir,
        "--verbosity", "quiet"
    )
    if (-not $Fix) {
        $arguments += "--verify-no-changes"
    }

    $ErrorActionPreference = "Continue"
    & dotnet @arguments 2>&1 | Out-Null
    $formatExitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    if ($formatExitCode -ne 0 -and -not (Test-Path -LiteralPath (Join-Path $reportDir "format-report.json"))) {
        throw "C# formatter could not run."
    }

    $reportPath = Join-Path $reportDir "format-report.json"
    if (Test-Path -LiteralPath $reportPath) {
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        foreach ($file in $report) {
            Write-Output $file.FilePath.Substring($projectDir.Length + 1)
        }
    }

    $failed = $formatExitCode -ne 0
    $folders = @('Runtime', 'Editor', 'Samples~')
    foreach ($folder in $folders) {
        $root = Join-Path $projectDir $folder
        foreach ($file in (Get-ChildItem -LiteralPath $root -Filter '*.cs' -File -Recurse)) {
            $path = $file.FullName.Substring($projectDir.Length + 1)
            $number = 0
            foreach ($line in (Get-Content -LiteralPath $file.FullName)) {
                $number++
                if ($line.Length -gt 120) { Write-Output "${path}:${number}: warning: line exceeds 120 characters" }
                if ($line -match '^\s*using\s+(?:static\s+)?System\.Linq\b' -or $line -match '\bSystem\.Linq\.' -or $line -match '^\s*from\s+\w+\s+in\b') {
                    Write-Output "${path}:${number}: warning: LINQ is not allowed"
                }
            }
        }
    }
    if ($failed) { throw "C# formatting failed. Run .\lint-csharp.ps1 -Fix for formatting." }
    Write-Output "C# lint passed for Runtime, Editor, and Samples~."
} finally {
    if (Test-Path -LiteralPath $reportDir) {
        Remove-Item -LiteralPath $reportDir -Recurse -Force
    }
}
