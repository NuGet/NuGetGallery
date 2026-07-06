<#
.SYNOPSIS
    Copies the .nupkg blobs for every package in a data export from a source Azure
    Blob Storage account/container (anonymous access) to a destination account using
    azcopy with Azure AD authentication.

.DESCRIPTION
    Reads Packages.json (and PackageRegistrations.json) from a folder produced by
    Export-SampleData.ps1, computes each blob name as

        {id-lower}.{normalizedVersion-lower}.nupkg

    (all packages live at the root of the container, no virtual directories), and copies
    them to the destination, preserving the container name and root path.

    Source authentication: none (the source container is assumed to allow anonymous /
    public blob access, per requirement).

    Destination authentication: Azure AD via azcopy. Run `azcopy login` beforehand, or pass
    -AutoLoginType (sets AZCOPY_AUTO_LOGIN_TYPE, e.g. AZCLI, MSI, DEVICE) for this process.

    The destination container is created (azcopy make) with the same name as the source
    container if it does not already exist.

.EXAMPLE
    az login
    .\Copy-SamplePackages.ps1 -InputDirectory .\out\export-20240101-120000 `
        -SourceAccount nugetprod -SourceContainer packages `
        -DestinationAccount mytestgallery -AutoLoginType AZCLI

.EXAMPLE
    .\Copy-SamplePackages.ps1 -InputDirectory .\export -SourceAccount src -SourceContainer packages `
        -DestinationAccount dst -DryRun
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputDirectory,

    [Parameter(Mandatory)][string]$SourceAccount,
    [Parameter(Mandatory)][string]$SourceContainer,

    [Parameter(Mandatory)][string]$DestinationAccount,
    [string]$DestinationContainer,

    [string]$EndpointSuffix = 'core.windows.net',

    # Optional explicit URL overrides (e.g. to embed a SAS). When set they take precedence
    # over the account/container parameters above.
    [string]$SourceContainerUrl,
    [string]$DestinationContainerUrl,

    [ValidateSet('AZCLI', 'MSI', 'DEVICE', 'PSCRED', 'WORKLOAD')][string]$AutoLoginType,

    [string]$AzCopyPath = 'azcopy',
    [ValidateSet('true', 'false', 'prompt', 'ifSourceNewer')][string]$Overwrite = 'ifSourceNewer',
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'DataSampling.Common.psm1') -Force

if (-not $DestinationContainer) { $DestinationContainer = $SourceContainer }
if (-not $SourceContainerUrl) { $SourceContainerUrl = "https://$SourceAccount.blob.$EndpointSuffix/$SourceContainer" }
if (-not $DestinationContainerUrl) { $DestinationContainerUrl = "https://$DestinationAccount.blob.$EndpointSuffix/$DestinationContainer" }

# ---------------------------------------------------------------------------
# Validate azcopy availability
# ---------------------------------------------------------------------------
$azcopy = Get-Command $AzCopyPath -ErrorAction SilentlyContinue
if (-not $azcopy) {
    throw "azcopy was not found (looked for '$AzCopyPath'). Install azcopy and ensure it is on PATH, or pass -AzCopyPath."
}
Write-DsLog "Using azcopy at '$($azcopy.Source)'."

if ($AutoLoginType) {
    $env:AZCOPY_AUTO_LOGIN_TYPE = $AutoLoginType
    Write-DsLog "Set AZCOPY_AUTO_LOGIN_TYPE=$AutoLoginType for destination authentication."
}

# ---------------------------------------------------------------------------
# Load package list from the export
# ---------------------------------------------------------------------------
$InputDirectory = (Resolve-Path $InputDirectory).Path
$packagesPath = Join-Path $InputDirectory 'Packages.json'
if (-not (Test-Path -LiteralPath $packagesPath)) {
    throw "Packages.json not found in $InputDirectory. Nothing to copy."
}

$packages = @(Read-DsJsonFile -Path $packagesPath)
Write-DsLog "Loaded $($packages.Count) package row(s) from export."

# Build PackageRegistrationKey -> Id map to resolve package Ids that are null on the Package row.
$registrationIdByKey = @{}
$registrationsPath = Join-Path $InputDirectory 'PackageRegistrations.json'
if (Test-Path -LiteralPath $registrationsPath) {
    foreach ($registration in @(Read-DsJsonFile -Path $registrationsPath)) {
        $registrationIdByKey[[int]$registration.Key] = [string]$registration.Id
    }
}

function Get-DsPackageId {
    param($Package)
    if ($Package.PSObject.Properties['Id'] -and -not [string]::IsNullOrEmpty($Package.Id)) {
        return [string]$Package.Id
    }
    if ($Package.PSObject.Properties['PackageRegistrationKey']) {
        $key = [int]$Package.PackageRegistrationKey
        if ($registrationIdByKey.ContainsKey($key)) { return $registrationIdByKey[$key] }
    }
    return $null
}

function Get-DsNormalizedVersion {
    param($Package)
    if ($Package.PSObject.Properties['NormalizedVersion'] -and -not [string]::IsNullOrEmpty($Package.NormalizedVersion)) {
        return [string]$Package.NormalizedVersion
    }
    # Best-effort fallback: strip build metadata and trailing zero revision. Proper SemVer
    # normalization requires the NuGet libraries; NormalizedVersion is virtually always present.
    if ($Package.PSObject.Properties['Version'] -and -not [string]::IsNullOrEmpty($Package.Version)) {
        Write-DsLog "Package '$(Get-DsPackageId -Package $Package)' has no NormalizedVersion; falling back to Version." 'WARN'
        return [string]$Package.Version
    }
    return $null
}

$blobNames = New-Object System.Collections.Generic.List[string]
$skipped = 0
foreach ($package in $packages) {
    $id = Get-DsPackageId -Package $package
    $version = Get-DsNormalizedVersion -Package $package
    if ([string]::IsNullOrEmpty($id) -or [string]::IsNullOrEmpty($version)) {
        Write-DsLog "Skipping a package row with unresolved Id/Version." 'WARN'
        $skipped++
        continue
    }
    $blobName = ("{0}.{1}.nupkg" -f $id, $version).ToLowerInvariant()
    $blobNames.Add($blobName)
}

$blobNames = @($blobNames | Select-Object -Unique)
Write-DsLog "Resolved $($blobNames.Count) unique .nupkg blob name(s) to copy ($skipped skipped)."
if ($blobNames.Count -eq 0) {
    Write-DsLog "Nothing to copy."
    return
}

# ---------------------------------------------------------------------------
# Write the list-of-files file (relative blob paths at container root)
# ---------------------------------------------------------------------------
$listFile = Join-Path $InputDirectory 'azcopy-list.txt'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($listFile, $blobNames, $utf8NoBom)
Write-DsLog "Wrote azcopy list-of-files to '$listFile'."

# ---------------------------------------------------------------------------
# Ensure destination container exists
# ---------------------------------------------------------------------------
Write-DsLog "Ensuring destination container exists: $DestinationContainerUrl"
if ($DryRun) {
    Write-DsLog "[DryRun] Would run: $($azcopy.Source) make `"$DestinationContainerUrl`"" 'INFO'
}
else {
    & $azcopy.Source make $DestinationContainerUrl 2>&1 | ForEach-Object { Write-DsLog "  azcopy make: $_" 'DEBUG' }
    # azcopy make returns non-zero if the container already exists; that is acceptable.
    Write-DsLog "Destination container ensured (existing containers are fine)."
}

# ---------------------------------------------------------------------------
# Copy
# ---------------------------------------------------------------------------
$azcopyArgs = @(
    'copy',
    $SourceContainerUrl,
    $DestinationContainerUrl,
    "--list-of-files=$listFile",
    "--overwrite=$Overwrite",
    '--log-level=INFO'
)
if ($DryRun) { $azcopyArgs += '--dry-run' }

Write-DsLog "Starting copy of $($blobNames.Count) blob(s) from '$SourceContainerUrl' to '$DestinationContainerUrl'."
Write-DsLog "Command: $($azcopy.Source) $($azcopyArgs -join ' ')"

& $azcopy.Source @azcopyArgs
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0) {
    throw "azcopy copy failed with exit code $exitCode. See azcopy output / job logs above."
}

Write-DsLog "Copy completed successfully."
if ($DryRun) { Write-DsLog "(this was a dry run; no data was transferred)" }
