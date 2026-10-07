[CmdletBinding()]
param(
    [ValidateSet('AssetStore')]
    [string]$Target = 'AssetStore',
    [string]$OutputPath,
    [string]$CreditsPath,
    [switch]$InPlace
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-ReleaseText {
    param([string]$Path, [string]$Content)

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Replace-ReleaseText {
    param([string]$Content, [string]$Pattern, [string]$Replacement)

    $expression = [regex]::new($Pattern)
    if ($expression.Matches($Content).Count -ne 1) {
        throw "Expected exactly one match for release transformation: $Pattern"
    }

    return $expression.Replace($Content, $Replacement)
}

function Get-ReleaseReadme {
    param([string]$Path)

    $content = [System.IO.File]::ReadAllText($Path)
    $content = Replace-ReleaseText $content 'alt="Embeds a CEF[^"\r\n]*"' (
        'alt="Ceffy browser integration"'
    )
    $badges = @('Unity 6+', 'License: MIT', 'openupm')
    foreach ($badge in $badges) {
        $pattern = '(?m)^\[!\[' + [regex]::Escape($badge) + '\][^\r\n]*\r?\n'
        $content = Replace-ReleaseText $content $pattern ''
    }

    $content = Replace-ReleaseText $content '(?m)^- \*\*Git LFS\*\*[^\r\n]*\r?\n' ''
    $content = Replace-ReleaseText $content '(?m)^Embeds a CEF[^\r\n]*' (
        'Embeds a CEF (Chromium Embedded Framework) browser inside Unity via a native C++ plugin.'
    )
    $installation = @'
## Installation

Install Ceffy from the Unity Asset Store using the Package Manager. Install the Unity Registry
dependencies listed in `package.json` if they are not already present in the project.

'@
    $content = Replace-ReleaseText $content '(?ms)^## Installation\r?\n.*?(?=^## Quick Start)' (
        $installation + "`n"
    )
    $content = Replace-ReleaseText $content '(?ms)^## Development\r?\n.*?(?=^## Testing)' ''
    $content = Replace-ReleaseText $content '(?m)^Ceffy is available under the \[MIT License\][^\r\n]*' (
        'Ceffy is licensed under the [Unity Asset Store EULA](https://unity.com/legal/as-terms). ' +
        'Third-party components retain their respective licenses; see ' +
        '[THIRD PARTY NOTICES.md](THIRD%20PARTY%20NOTICES.md) and ' +
        '[Chromium credits](ThirdPartyLicenses/ChromiumCredits.html).'
    )

    if ($content -match 'License: MIT|MIT License|LICENSE\.md|logo=unity|openupm|Git LFS') {
        throw 'The exported README still contains excluded distribution or licensing references.'
    }

    return $content
}

function Assert-ReleaseSource {
    param([string]$Path)

    $entry = Get-Item -LiteralPath $Path
    if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Release sources must not contain symbolic links or junctions: $Path"
    }

    if ($entry.PSIsContainer) {
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force) {
            Assert-ReleaseSource $child.FullName
        }
        return
    }

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $buffer = [byte[]]::new(128)
        $length = $stream.Read($buffer, 0, $buffer.Length)
        $prefix = [System.Text.Encoding]::ASCII.GetString($buffer, 0, $length)
        if ($prefix.StartsWith('version https://git-lfs.github.com/spec/v1')) {
            throw "Download the Git LFS file before exporting: $Path"
        }
    }
    finally {
        $stream.Dispose()
    }
}

$sourceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$manifest = Get-Content -LiteralPath (Join-Path $sourceRoot 'package.json') -Raw | ConvertFrom-Json
if ($InPlace -and -not [string]::IsNullOrWhiteSpace($OutputPath)) {
    throw 'Use either -InPlace or -OutputPath, not both.'
}

if ($InPlace) {
    $OutputPath = $sourceRoot
}
elseif ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $releaseName = 'ceffy-{0}-{1}-{2}' -f $manifest.version, $Target, (Get-Date -Format 'yyyyMMdd-HHmmss')
    $OutputPath = Join-Path (Split-Path $sourceRoot -Parent) "ceffy-releases/$releaseName"
}

$destination = [System.IO.Path]::GetFullPath($OutputPath)
$sourcePrefix = $sourceRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$destinationPrefix = $destination.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $InPlace -and ($destination.Equals($sourceRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $destination.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    $sourceRoot.StartsWith($destinationPrefix, [System.StringComparison]::OrdinalIgnoreCase))) {
    throw 'The export folder must be outside the repository and must not be its ancestor.'
}
if (-not $InPlace -and (Test-Path -LiteralPath $destination)) {
    throw "The export folder already exists. Choose a fresh OutputPath: $destination"
}

if ([string]::IsNullOrWhiteSpace($CreditsPath)) {
    $CreditsPath = Join-Path $sourceRoot 'ThirdPartyLicenses/ChromiumCredits.html'
}
if (-not (Test-Path -LiteralPath $CreditsPath -PathType Leaf)) {
    throw 'CEF credits are required. Supply -CreditsPath from the distribution matching the bundled runtime.'
}

$releaseEntries = @(
    'Runtime', 'Editor', 'NativeRuntime', 'Samples~', 'Tests', 'Documentation~', 'ThirdPartyLicenses',
    'package.json', 'README.md', 'CHANGELOG.md', 'THIRD PARTY NOTICES.md'
)
foreach ($releaseEntry in $releaseEntries) {
    Assert-ReleaseSource (Join-Path $sourceRoot $releaseEntry)
}
Assert-ReleaseSource $CreditsPath

$readme = Get-ReleaseReadme (Join-Path $sourceRoot 'README.md')
$demoPath = 'Samples~/Worldspace/WorldspaceDemo.cs'
$scenePath = 'Samples~/Worldspace/WorldspaceDemo.unity'
$demo = Replace-ReleaseText ([System.IO.File]::ReadAllText((Join-Path $sourceRoot $demoPath))) (
    '(?m)^([ \t]*public string StartUrl = )"[^"\r\n]*";'
) '$1"https://www.hovgaard.com/";'
$demo = $demo.TrimEnd("`r", "`n") + "`r`n"
$scene = Replace-ReleaseText ([System.IO.File]::ReadAllText((Join-Path $sourceRoot $scenePath))) (
    '(?m)^([ \t]*StartUrl: )[^\r\n]*'
) '${1}https://www.hovgaard.com/'

if (-not $InPlace) {
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($releaseEntry in $releaseEntries) {
        $sourcePath = Join-Path $sourceRoot $releaseEntry
        Copy-Item -LiteralPath $sourcePath -Destination $destination -Recurse
        $metaPath = $sourcePath + '.meta'
        if (Test-Path -LiteralPath $metaPath -PathType Leaf) {
            Copy-Item -LiteralPath $metaPath -Destination $destination
        }
    }
}

Write-ReleaseText (Join-Path $destination 'README.md') $readme
Write-ReleaseText (Join-Path $destination $demoPath) $demo
Write-ReleaseText (Join-Path $destination $scenePath) $scene
$licensesPath = Join-Path $destination 'ThirdPartyLicenses'
$exportCreditsPath = Join-Path $licensesPath 'ChromiumCredits.html'
if (-not [System.IO.Path]::GetFullPath($CreditsPath).Equals(
    $exportCreditsPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $CreditsPath -Destination $exportCreditsPath
}

if ($InPlace) {
    foreach ($licenseEntry in @('LICENSE.md', 'LICENSE.md.meta')) {
        $licensePath = Join-Path $destination $licenseEntry
        if (Test-Path -LiteralPath $licensePath -PathType Leaf) {
            Remove-Item -LiteralPath $licensePath
        }
    }
}

foreach ($excludedEntry in @('LICENSE.md', 'LICENSE.md.meta', 'native~', '.git', 'prepare-release.ps1')) {
    if ($InPlace -and $excludedEntry -in @('native~', '.git', 'prepare-release.ps1')) {
        continue
    }
    if (Test-Path -LiteralPath (Join-Path $destination $excludedEntry)) {
        throw "Unexpected entry in export: $excludedEntry"
    }
}
$null = Get-Content -LiteralPath (Join-Path $destination 'package.json') -Raw | ConvertFrom-Json
if ([System.IO.File]::ReadAllText((Join-Path $destination $demoPath)) -notmatch
    'public string StartUrl = "https://www\.hovgaard\.com/";' -or
    [System.IO.File]::ReadAllText((Join-Path $destination $scenePath)) -notmatch
    '(?m)^  StartUrl: https://www\.hovgaard\.com/\r?$') {
    throw 'Worldspace URLs were not exported correctly.'
}

Write-Output "Prepared $Target package: $destination"
if ($InPlace) {
    Write-Output 'Updated the customized package directly and removed LICENSE.md and LICENSE.md.meta.'
    Write-Output 'Remove the copied prepare-release.ps1 and its .meta before uploading the package.'
}
Write-Warning 'Confirm the CEF credits match the shipped binaries and review all dependency license obligations.'
Write-Warning 'Review marketing images, listing disclosures, and CEF subprocess acceptance before submission.'
Write-Warning 'Verify installation and samples in a clean Unity project. This export does not certify store approval.'
