<#
.SYNOPSIS
    Builds the solution and runs all tests with .NET 10 on Windows, Linux, or macOS.

.DESCRIPTION
    PDFsharp and MigraDoc have one code base for Windows, Linux, and macOS, and target net10.0 only.
    The script builds and tests the same solution on every operating system,
    like the CI workflow in .github/workflows/ci.yml does.

    The script replaces the former version, which compared .NET Framework 4.6.2, .NET 6, and .NET 8
    test results in Windows and WSL side by side.

.PARAMETER Config
    The configuration to build and test ("Debug" or "Release"). "Debug" is the default.

.PARAMETER SkipBuild
    Skips the build.

.PARAMETER RunAllTests
    Runs the slow tests, too (environment variable PDFsharpTests=runalltests).

.EXAMPLE
    ./dev/run-tests.ps1
    ./dev/run-tests.ps1 -Config Release -RunAllTests
#>

#Requires -Version 7
#Requires -PSEdition Core

param (
    [Parameter(Mandatory = $false)] [ValidateSet('Debug', 'Release')] [string]$Config = 'Debug',
    [switch]$SkipBuild,
    [switch]$RunAllTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $root 'TestResults'

# The solution to build and test. It is the same on all operating systems.
$solution = Join-Path $root 'PdfSharp.sln'

$env:PDFsharpTests = if ($RunAllTests) { 'runalltests' } else { $null }

if (-not $SkipBuild) {
    # PdfSharp.BuildConfig downloads the assets if necessary and generates the version information.
    dotnet build (Join-Path $root 'src/foundation/src/shared/src/PdfSharp.BuildConfig/PdfSharp.BuildConfig.csproj') -c $Config
    if ($LASTEXITCODE -ne 0) { throw 'Building PdfSharp.BuildConfig failed.' }

    dotnet build $solution -c $Config
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

if (Test-Path $resultsDirectory) {
    Remove-Item $resultsDirectory -Recurse -Force
}

dotnet test $solution -c $Config --no-build --logger trx --results-directory $resultsDirectory
$testExitCode = $LASTEXITCODE

# Summarize the failed tests of all test projects.
$failed = foreach ($trx in Get-ChildItem $resultsDirectory -Filter *.trx -ErrorAction SilentlyContinue) {
    [xml]$xml = Get-Content -Raw -LiteralPath $trx.FullName
    foreach ($result in @($xml.TestRun.Results.UnitTestResult) | Where-Object { $_.outcome -eq 'Failed' }) {
        $message = ("$($result.Output.ErrorInfo.Message)" -split "`n")[0].Trim()
        [pscustomobject]@{ Test = $result.testName; Error = $message }
    }
}

Write-Output ''
if ($failed) {
    Write-Output "Failed tests: $(@($failed).Count)"
    $failed | Sort-Object Test -Unique | Format-Table -AutoSize -Wrap
}
else {
    Write-Output 'All tests passed.'
}

exit $testExitCode
