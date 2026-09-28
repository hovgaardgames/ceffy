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

    if ($formatExitCode -ne 0) {
        throw "C# formatting failed. Run .\lint-csharp.ps1 -Fix to format the listed files."
    }

    Write-Output "C# formatting passed for Runtime and Editor."
} finally {
    if (Test-Path -LiteralPath $reportDir) {
        Remove-Item -LiteralPath $reportDir -Recurse -Force
    }
}
