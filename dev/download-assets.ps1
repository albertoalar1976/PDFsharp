<#
.SYNOPSIS
    Downloads the assets that the tests need: fonts, images, and PDF files.

.DESCRIPTION
    Building PDFsharp and MigraDoc does not need the assets. Only tests, test apps, and samples use them.
    They are stored in the folder 'assets' in the repository root, which is not under source control.

    By default, the assets are downloaded from https://assets.pdfsharp.net/, the server of the upstream project.
    To use a mirror, set the environment variable PDFSHARP_ASSETS_URL or use the parameter -Source.

    Every downloaded file is checked against its SHA-256 hash before it is used. If a file on the server
    has changed, the script stops and leaves the existing assets untouched. The hashes are those of
    assets version 1034, with which all tests pass on Windows, Linux, and macOS.

.PARAMETER Source
    The base URL of the assets, e.g. https://example.com/pdfsharp-assets/

.EXAMPLE
    ./dev/download-assets.ps1
    ./dev/download-assets.ps1 -Source https://example.com/pdfsharp-assets/
#>

#Requires -Version 7
#Requires -PSEdition Core

param (
    [string]$Source = $(if ($env:PDFSHARP_ASSETS_URL) { $env:PDFSHARP_ASSETS_URL } else { 'https://assets.pdfsharp.net/' })
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'  # The progress bar slows down Invoke-WebRequest considerably.

if (-not $Source.EndsWith('/')) { $Source += '/' }

$assetsVersion = '1034'

# Archives that are extracted to assets/<folder of the archive>: path relative to the source => SHA-256 hash.
$archives = [ordered]@{
    'pdfsharp/pdfsharp.zip'                = 'b66f84b06a974043ff6db0da2e4752ab82399dcdfa01924846484018d3d2db16'
    'migradoc/migradoc.zip'                = 'bbd056dc83bd5468e9d4bd2b4872a16f91171d171d0d172929712bf6928b2f94'
    'pdfsharp-6.x/pdfsharp-6.x.zip'        = '2e8c5049b8b8acfcc3e02687deddb3b8eb79b211d9cdf26366406f721778dcc6'
    'archives/grammar-by-example/GBE.zip'  = 'c786966eeb652355ac76f91b0c6e81e28d7b86afa585d9b1aac139b260c042ec'
    'archives/samples-1.5/samples-1.5.zip' = 'df516b0bb77553c3c011af7b44b8ed1f3c4addb3e39bbab68cb23b56af911465'
}

# The Noto Sans fonts (SIL Open Font License). Only the static fonts are used.
$notoArchive = 'fonts/Noto/Noto_Sans.zip'
$notoHash = 'db36f285c72763d6f6dbb1571530c21244f260eab211d6a9d4da2a169aed1066'

$destination = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../assets'))
$download = Join-Path ([IO.Path]::GetTempPath()) "pdfsharp-assets-$PID"

# Downloads a file to the temporary folder and checks its hash. Returns the path of the file.
function Get-CheckedFile([string]$path, [string]$expectedHash) {
    $file = Join-Path $download ($path -replace '/', '_')
    Write-Host "Downloading $Source$path"
    Invoke-WebRequest -Uri "$Source$path" -OutFile $file
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash
    if ($hash -ne $expectedHash) {  # -ne ignores case.
        throw "The SHA-256 hash of '$path' is $hash, but $expectedHash was expected. " +
              "The file on the server has changed. Use a mirror with the expected files (parameter -Source or environment variable PDFSHARP_ASSETS_URL)."
    }
    return $file
}

New-Item -ItemType Directory -Path $download -Force | Out-Null
try {
    # Download and check all files first, so that a failure leaves the existing assets untouched.
    $files = [ordered]@{}
    foreach ($archive in $archives.GetEnumerator()) {
        $files[$archive.Key] = Get-CheckedFile $archive.Key $archive.Value
    }
    $notoFile = Get-CheckedFile $notoArchive $notoHash

    if (Test-Path -PathType Container $destination) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }
    New-Item -ItemType Directory -Path $destination | Out-Null

    foreach ($file in $files.GetEnumerator()) {
        $folder = Join-Path $destination ([IO.Path]::GetDirectoryName($file.Key))
        Expand-Archive -LiteralPath $file.Value -DestinationPath $folder -Force
        # Not all archives contain compress.ps1.
        Remove-Item -LiteralPath (Join-Path $folder 'compress.ps1') -ErrorAction Ignore
    }

    $notoTemp = Join-Path $download 'noto'
    Expand-Archive -LiteralPath $notoFile -DestinationPath $notoTemp -Force
    $notoDestination = Join-Path $destination 'fonts/Noto/Noto_Sans/static'
    New-Item -ItemType Directory -Path $notoDestination -Force | Out-Null
    foreach ($fontFolder in 'NotoSans', 'NotoSans_Condensed', 'NotoSans_ExtraCondensed', 'NotoSans_SemiCondensed') {
        Copy-Item -Path (Join-Path $notoTemp "static/$fontFolder/*") -Include '*.ttf' -Destination $notoDestination
    }

    Set-Content -LiteralPath (Join-Path $destination '.assets-version') -Value $assetsVersion -NoNewline
    Write-Output "Assets version $assetsVersion are in $destination"
}
finally {
    Remove-Item -LiteralPath $download -Recurse -Force -ErrorAction Ignore
}
