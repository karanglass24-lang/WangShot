<#
.SYNOPSIS
    Creates the portable ZIP files (full and light) next to the installers of a local Release build.

.DESCRIPTION
    Uses prepare-portable.ps1, like the release workflow. The ZIP files are named after the installer that was just
    built: WangShot-<version>-setup.exe gives WangShot-PORTABLE-<version>.zip and WangShot-Light-PORTABLE-<version>.zip.
    WangShot Light is its own build of WangShot.exe (bin\Release-Light, see Greenshot.csproj).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRootPath,
    [Parameter(Mandatory = $true)]
    [string]$BuildArtifactsPath,
    [Parameter(Mandatory = $true)]
    [string]$LightBuildArtifactsPath,
    [Parameter(Mandatory = $true)]
    [string]$InstallerDir
)

$ErrorActionPreference = 'Stop'

$installer = Get-ChildItem -Path $InstallerDir -Filter 'WangShot-*-setup.exe' | Where-Object { $_.Name -notlike 'WangShot-Light-*' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $installer) {
    throw "No WangShot-*-setup.exe found in $InstallerDir"
}
if ($installer.BaseName -notmatch '^WangShot-(?<version>.+)-setup$') {
    throw "Could not read the version from $($installer.Name)"
}
$version = $Matches['version']

$prepareScript = Join-Path $RepositoryRootPath 'prepare-portable.ps1'
foreach ($edition in @(@{ Prefix = 'WangShot'; Light = $false; Path = $BuildArtifactsPath }, @{ Prefix = 'WangShot-Light'; Light = $true; Path = $LightBuildArtifactsPath })) {
    $staging = Join-Path ([System.IO.Path]::GetTempPath()) ("wangshot-portable-" + [guid]::NewGuid().ToString('N'))
    try {
        & $prepareScript -RepositoryRootPath $RepositoryRootPath -BuildArtifactsPath $edition.Path -OutputPath $staging -Light:$edition.Light
        $zip = Join-Path $InstallerDir ("{0}-PORTABLE-{1}.zip" -f $edition.Prefix, $version)
        Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
        Write-Host "Created $zip"
    }
    finally {
        Remove-Item -Path $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}
