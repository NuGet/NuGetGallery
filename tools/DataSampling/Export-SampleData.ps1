<#
.SYNOPSIS
    Samples NuGet Gallery package data from a (read-only) SQL Server database and
    writes it to relationship-preserving JSON files for later import.

.DESCRIPTION
    Selects a set of packages using up to three independent rules (any combination):

      1. -PackageIdsFile     UTF-8 text file, one PackageRegistration.Id per line.
                             Exports ALL non-deleted versions (PackageStatusKey <> Deleted).

      2. -OwnerUsernamesFile UTF-8 text file, one Username per line (users OR organizations,
                             both live in dbo.Users). Exports ALL non-deleted versions of every
                             package registration owned by those accounts.

      3. -SamplePercentage   0-100. From registrations that have at least one Listed version,
                             randomly selects that percentage and exports their Listed,
                             non-deleted versions. Use -Seed for reproducible sampling.

    The selected package graph is closed over its owned/outbound relationships (owners, required
    signers, reserved namespaces, dependencies, frameworks, types, authors, histories, symbol
    packages, deprecations + alternates, renames, vulnerabilities, certificates, uploader/member
    users, organization memberships, roles, security policies). Auth secrets
    (Credentials, Scopes, FederatedCredentials, FederatedCredentialPolicies) and the 'Admins' role
    (plus its UserRoles rows) are never exported.

    Output: one folder containing manifest.json plus one <TableName>.json per non-empty table.
    Original key values are retained only as correlation handles; Import-SampleData.ps1 regenerates
    real keys while preserving relationships.

.EXAMPLE
    .\Export-SampleData.ps1 -ConnectionString "Server=.;Database=NuGetGallery;Integrated Security=SSPI" `
        -PackageIdsFile ids.txt -OwnerUsernamesFile owners.txt -SamplePercentage 5 -Seed 42 `
        -OutputDirectory .\out
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ConnectionString,

    [string]$PackageIdsFile,
    [string]$OwnerUsernamesFile,

    [ValidateRange(0, 100)][double]$SamplePercentage = 0,
    [int]$Seed,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot ("out\export-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))),

    [int]$CommandTimeoutSeconds = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'DataSampling.Common.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'DataSampling.Schema.psm1') -Force

if (-not $PackageIdsFile -and -not $OwnerUsernamesFile -and $SamplePercentage -le 0) {
    throw "No selection rule supplied. Provide at least one of -PackageIdsFile, -OwnerUsernamesFile, or -SamplePercentage."
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

$PackageStatusDeleted = 1

function New-IntSet { return New-Object 'System.Collections.Generic.HashSet[int]' }

function Set-DsIntTempTable {
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$TableName,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[int]]$Values
    )

    Invoke-DsNonQuery -Connection $Connection -TimeoutSeconds $CommandTimeoutSeconds `
        -Query "IF OBJECT_ID('tempdb..$TableName') IS NOT NULL DROP TABLE $TableName; CREATE TABLE $TableName ([Key] INT PRIMARY KEY);" | Out-Null

    if ($Values.Count -eq 0) { return }

    $array = @($Values)
    $batchSize = 1000
    for ($start = 0; $start -lt $array.Count; $start += $batchSize) {
        $end = [Math]::Min($start + $batchSize, $array.Count) - 1
        $rowClauses = New-Object System.Collections.Generic.List[string]
        for ($i = $start; $i -le $end; $i++) {
            $rowClauses.Add("($($array[$i]))")
        }
        $sql = "INSERT INTO $TableName ([Key]) VALUES " + ($rowClauses -join ',')
        Invoke-DsNonQuery -Connection $Connection -Query $sql -TimeoutSeconds $CommandTimeoutSeconds | Out-Null
    }
}

function Add-KeysFromQuery {
    <#
        Runs a query that returns one or more INT columns and adds every non-null value
        into the matching target HashSet. $ColumnToSet maps column name -> HashSet.
        Returns the number of newly added keys across all sets.
    #>
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Query,
        [Parameter(Mandatory)][hashtable]$ColumnToSet
    )

    $added = 0
    $rows = Invoke-DsQuery -Connection $Connection -Query $Query -TimeoutSeconds $CommandTimeoutSeconds
    foreach ($row in $rows) {
        foreach ($column in $ColumnToSet.Keys) {
            $value = $row.$column
            if ($null -ne $value) {
                if ($ColumnToSet[$column].Add([int]$value)) { $added++ }
            }
        }
    }
    return $added
}

# ---------------------------------------------------------------------------
# Connect
# ---------------------------------------------------------------------------

Write-DsLog "Connecting to source database (read-only)..."
$connection = New-DsSqlConnection -ConnectionString $ConnectionString -ReadOnly
try {
    $databaseName = $connection.Database
    Write-DsLog "Connected to database '$databaseName'."

    # Key sets that participate in transitive closure.
    $regKeys        = New-IntSet   # PackageRegistrations
    $pkgKeys        = New-IntSet   # Packages
    $userKeys       = New-IntSet   # Users (incl. organizations)
    $certKeys       = New-IntSet   # Certificates
    $reservedNsKeys = New-IntSet   # ReservedNamespaces
    $vulnRangeKeys  = New-IntSet   # VulnerablePackageVersionRanges

    # Detect the Admins role so we can exclude it and its UserRoles rows.
    $adminsRoleKey = Invoke-DsScalar -Connection $connection -Query "SELECT [Key] FROM dbo.Roles WHERE Name = 'Admins'" -TimeoutSeconds $CommandTimeoutSeconds
    if ($null -ne $adminsRoleKey) {
        Write-DsLog "Detected 'Admins' role with Key=$adminsRoleKey (will be excluded)."
    }
    else {
        Write-DsLog "No 'Admins' role found."
    }

    $criteria = [ordered]@{}

    # -----------------------------------------------------------------------
    # Rule 1: package IDs
    # -----------------------------------------------------------------------
    if ($PackageIdsFile) {
        $ids = Read-DsLineList -Path $PackageIdsFile
        Write-DsLog "Rule 1: $($ids.Count) package id(s) supplied."
        $criteria['packageIdsFile'] = (Resolve-Path $PackageIdsFile).Path
        $criteria['packageIdsCount'] = $ids.Count

        [void](New-DsStringTempTable -Connection $connection -Values $ids -TableName '#Ids' -MaxLength 128)

        Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $regKeys } -Query @"
SELECT pr.[Key] AS [Key]
FROM dbo.PackageRegistrations pr
JOIN #Ids f ON pr.Id = f.Value
"@ | Out-Null

        Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $pkgKeys } -Query @"
SELECT p.[Key] AS [Key]
FROM dbo.Packages p
JOIN dbo.PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]
JOIN #Ids f ON pr.Id = f.Value
WHERE p.PackageStatusKey <> $PackageStatusDeleted
"@ | Out-Null

        Write-DsLog "Rule 1: matched $($regKeys.Count) registration(s), $($pkgKeys.Count) package version(s)."
    }

    # -----------------------------------------------------------------------
    # Rule 2: owner usernames
    # -----------------------------------------------------------------------
    if ($OwnerUsernamesFile) {
        $usernames = Read-DsLineList -Path $OwnerUsernamesFile
        Write-DsLog "Rule 2: $($usernames.Count) owner username(s) supplied."
        $criteria['ownerUsernamesFile'] = (Resolve-Path $OwnerUsernamesFile).Path
        $criteria['ownerUsernamesCount'] = $usernames.Count

        [void](New-DsStringTempTable -Connection $connection -Values $usernames -TableName '#Owners' -MaxLength 64)

        $regBefore = $regKeys.Count
        $pkgBefore = $pkgKeys.Count

        Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $userKeys } -Query @"
SELECT u.[Key] AS [Key]
FROM dbo.Users u
JOIN #Owners f ON u.Username = f.Value
"@ | Out-Null

        Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $regKeys } -Query @"
SELECT DISTINCT o.PackageRegistrationKey AS [Key]
FROM dbo.PackageRegistrationOwners o
JOIN dbo.Users u ON o.UserKey = u.[Key]
JOIN #Owners f ON u.Username = f.Value
"@ | Out-Null

        Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $pkgKeys } -Query @"
SELECT p.[Key] AS [Key]
FROM dbo.Packages p
JOIN dbo.PackageRegistrationOwners o ON p.PackageRegistrationKey = o.PackageRegistrationKey
JOIN dbo.Users u ON o.UserKey = u.[Key]
JOIN #Owners f ON u.Username = f.Value
WHERE p.PackageStatusKey <> $PackageStatusDeleted
"@ | Out-Null

        Write-DsLog "Rule 2: added $($regKeys.Count - $regBefore) registration(s), $($pkgKeys.Count - $pkgBefore) package version(s)."
    }

    # -----------------------------------------------------------------------
    # Rule 3: percentage sampling over registrations with >=1 listed version
    # -----------------------------------------------------------------------
    if ($SamplePercentage -gt 0) {
        Write-DsLog "Rule 3: sampling $SamplePercentage% of registrations with a listed version."
        $criteria['samplePercentage'] = $SamplePercentage
        if ($PSBoundParameters.ContainsKey('Seed')) { $criteria['seed'] = $Seed }

        $candidates = Invoke-DsQuery -Connection $connection -TimeoutSeconds $CommandTimeoutSeconds -Query @"
SELECT DISTINCT pr.[Key] AS [Key]
FROM dbo.PackageRegistrations pr
JOIN dbo.Packages p ON p.PackageRegistrationKey = pr.[Key]
WHERE p.Listed = 1
"@
        $candidateKeys = @($candidates | ForEach-Object { [int]$_.Key })
        $sampleCount = [int][Math]::Ceiling($candidateKeys.Count * ($SamplePercentage / 100.0))
        if ($sampleCount -gt $candidateKeys.Count) { $sampleCount = $candidateKeys.Count }
        Write-DsLog "Rule 3: $($candidateKeys.Count) candidate registration(s); selecting $sampleCount."

        $rng = if ($PSBoundParameters.ContainsKey('Seed')) { New-Object System.Random($Seed) } else { New-Object System.Random }
        # Fisher-Yates partial shuffle to pick $sampleCount items.
        $selected = New-IntSet
        $n = $candidateKeys.Count
        for ($i = 0; $i -lt $sampleCount; $i++) {
            $j = $rng.Next($i, $n)
            $tmp = $candidateKeys[$i]; $candidateKeys[$i] = $candidateKeys[$j]; $candidateKeys[$j] = $tmp
            [void]$selected.Add($candidateKeys[$i])
            [void]$regKeys.Add($candidateKeys[$i])
        }

        if ($selected.Count -gt 0) {
            Set-DsIntTempTable -Connection $connection -TableName '#Sampled' -Values $selected
            $pkgBefore = $pkgKeys.Count
            Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $pkgKeys } -Query @"
SELECT p.[Key] AS [Key]
FROM dbo.Packages p
JOIN #Sampled s ON p.PackageRegistrationKey = s.[Key]
WHERE p.Listed = 1 AND p.PackageStatusKey <> $PackageStatusDeleted
"@ | Out-Null
            Write-DsLog "Rule 3: added $($pkgKeys.Count - $pkgBefore) listed package version(s)."
        }
    }

    if ($pkgKeys.Count -eq 0 -and $regKeys.Count -eq 0) {
        throw "Selection produced no packages or registrations. Nothing to export."
    }

    # -----------------------------------------------------------------------
    # Transitive closure (fixpoint)
    # -----------------------------------------------------------------------
    Write-DsLog "Computing transitive closure of the selected package graph..."
    $iteration = 0
    do {
        $iteration++
        $added = 0

        Set-DsIntTempTable -Connection $connection -TableName '#Pkg'  -Values $pkgKeys
        Set-DsIntTempTable -Connection $connection -TableName '#Reg'  -Values $regKeys
        Set-DsIntTempTable -Connection $connection -TableName '#Usr'  -Values $userKeys
        Set-DsIntTempTable -Connection $connection -TableName '#Rns'  -Values $reservedNsKeys

        # Every package's own registration must exist (FK integrity).
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'Key' = $regKeys } -Query @"
SELECT DISTINCT p.PackageRegistrationKey AS [Key]
FROM dbo.Packages p JOIN #Pkg k ON p.[Key] = k.[Key]
WHERE p.PackageRegistrationKey IS NOT NULL
"@

        # Package -> uploader user, signing certificate.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'UserKey' = $userKeys; 'CertificateKey' = $certKeys } -Query @"
SELECT DISTINCT p.UserKey, p.CertificateKey
FROM dbo.Packages p JOIN #Pkg k ON p.[Key] = k.[Key]
"@

        # Package -> deprecation alternates + deprecating user.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'AlternatePackageKey' = $pkgKeys; 'AlternatePackageRegistrationKey' = $regKeys; 'DeprecatedByUserKey' = $userKeys } -Query @"
SELECT d.AlternatePackageKey, d.AlternatePackageRegistrationKey, d.DeprecatedByUserKey
FROM dbo.PackageDeprecations d JOIN #Pkg k ON d.PackageKey = k.[Key]
"@

        # Package -> vulnerable version ranges.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'VulnerablePackageVersionRange_Key' = $vulnRangeKeys } -Query @"
SELECT DISTINCT vp.VulnerablePackageVersionRange_Key
FROM dbo.VulnerablePackageVersionRangePackages vp JOIN #Pkg k ON vp.Package_Key = k.[Key]
"@

        # Registration -> owners, required signers.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'UserKey' = $userKeys } -Query @"
SELECT o.UserKey FROM dbo.PackageRegistrationOwners o JOIN #Reg k ON o.PackageRegistrationKey = k.[Key]
UNION
SELECT s.UserKey FROM dbo.PackageRegistrationRequiredSigners s JOIN #Reg k ON s.PackageRegistrationKey = k.[Key]
"@

        # Registration -> reserved namespaces.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'ReservedNamespaceKey' = $reservedNsKeys } -Query @"
SELECT DISTINCT rnr.ReservedNamespaceKey
FROM dbo.ReservedNamespaceRegistrations rnr JOIN #Reg k ON rnr.PackageRegistrationKey = k.[Key]
"@

        # Registration -> rename partners (both directions).
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'FromPackageRegistrationKey' = $regKeys; 'ToPackageRegistrationKey' = $regKeys } -Query @"
SELECT r.FromPackageRegistrationKey, r.ToPackageRegistrationKey
FROM dbo.PackageRenames r
JOIN #Reg k ON r.FromPackageRegistrationKey = k.[Key] OR r.ToPackageRegistrationKey = k.[Key]
"@

        # User -> certificates.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'CertificateKey' = $certKeys } -Query @"
SELECT DISTINCT uc.CertificateKey
FROM dbo.UserCertificates uc JOIN #Usr k ON uc.UserKey = k.[Key]
"@

        # User <-> organization memberships (both endpoints).
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'OrganizationKey' = $userKeys; 'MemberKey' = $userKeys } -Query @"
SELECT m.OrganizationKey, m.MemberKey
FROM dbo.Memberships m
JOIN #Usr k ON m.OrganizationKey = k.[Key] OR m.MemberKey = k.[Key]
"@

        # Reserved namespace -> owners.
        $added += Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'UserKey' = $userKeys } -Query @"
SELECT DISTINCT rno.UserKey
FROM dbo.ReservedNamespaceOwners rno JOIN #Rns k ON rno.ReservedNamespaceKey = k.[Key]
"@

        Write-DsLog "  iteration ${iteration}: +$added key(s) (reg=$($regKeys.Count) pkg=$($pkgKeys.Count) user=$($userKeys.Count) cert=$($certKeys.Count) ns=$($reservedNsKeys.Count) vrange=$($vulnRangeKeys.Count))"
    } while ($added -gt 0)

    # Derive leaf sets that do not expand further.
    Set-DsIntTempTable -Connection $connection -TableName '#Usr' -Values $userKeys
    Set-DsIntTempTable -Connection $connection -TableName '#VRange' -Values $vulnRangeKeys

    $vulnKeys = New-IntSet
    if ($vulnRangeKeys.Count -gt 0) {
        Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'VulnerabilityKey' = $vulnKeys } -Query @"
SELECT DISTINCT vr.VulnerabilityKey FROM dbo.VulnerablePackageVersionRanges vr JOIN #VRange k ON vr.[Key] = k.[Key]
"@ | Out-Null
    }

    $roleKeys = New-IntSet
    $adminsFilter = if ($null -ne $adminsRoleKey) { "AND ur.RoleKey <> $([int]$adminsRoleKey)" } else { '' }
    Add-KeysFromQuery -Connection $connection -ColumnToSet @{ 'RoleKey' = $roleKeys } -Query @"
SELECT DISTINCT ur.RoleKey
FROM dbo.UserRoles ur JOIN #Usr k ON ur.UserKey = k.[Key]
WHERE 1 = 1 $adminsFilter
"@ | Out-Null

    Write-DsLog "Closure complete: reg=$($regKeys.Count) pkg=$($pkgKeys.Count) user=$($userKeys.Count) cert=$($certKeys.Count) ns=$($reservedNsKeys.Count) vrange=$($vulnRangeKeys.Count) vuln=$($vulnKeys.Count) role=$($roleKeys.Count)."

    # -----------------------------------------------------------------------
    # Materialize all key sets as temp tables for the dump phase.
    # -----------------------------------------------------------------------
    Set-DsIntTempTable -Connection $connection -TableName '#Reg'    -Values $regKeys
    Set-DsIntTempTable -Connection $connection -TableName '#Pkg'    -Values $pkgKeys
    Set-DsIntTempTable -Connection $connection -TableName '#Usr'    -Values $userKeys
    Set-DsIntTempTable -Connection $connection -TableName '#Cert'   -Values $certKeys
    Set-DsIntTempTable -Connection $connection -TableName '#Rns'    -Values $reservedNsKeys
    Set-DsIntTempTable -Connection $connection -TableName '#VRange' -Values $vulnRangeKeys
    Set-DsIntTempTable -Connection $connection -TableName '#Vuln'   -Values $vulnKeys
    Set-DsIntTempTable -Connection $connection -TableName '#Role'   -Values $roleKeys

    # Table dump queries. Each must SELECT * so all columns are captured.
    # Join tables filter on BOTH endpoints to avoid dangling references.
    $dumpQueries = [ordered]@{
        'Roles'                                = "SELECT t.* FROM dbo.Roles t JOIN #Role k ON t.[Key] = k.[Key]"
        'Certificates'                         = "SELECT t.* FROM dbo.Certificates t JOIN #Cert k ON t.[Key] = k.[Key]"
        'PackageVulnerabilities'               = "SELECT t.* FROM dbo.PackageVulnerabilities t JOIN #Vuln k ON t.[Key] = k.[Key]"
        'Users'                                = "SELECT t.* FROM dbo.Users t JOIN #Usr k ON t.[Key] = k.[Key]"
        'Organizations'                        = "SELECT t.* FROM dbo.Organizations t JOIN #Usr k ON t.[Key] = k.[Key]"
        'ReservedNamespaces'                   = "SELECT t.* FROM dbo.ReservedNamespaces t JOIN #Rns k ON t.[Key] = k.[Key]"
        'PackageRegistrations'                 = "SELECT t.* FROM dbo.PackageRegistrations t JOIN #Reg k ON t.[Key] = k.[Key]"
        'Packages'                             = "SELECT t.* FROM dbo.Packages t JOIN #Pkg k ON t.[Key] = k.[Key]"
        'VulnerablePackageVersionRanges'       = "SELECT t.* FROM dbo.VulnerablePackageVersionRanges t JOIN #VRange k ON t.[Key] = k.[Key]"
        'PackageDependencies'                  = "SELECT t.* FROM dbo.PackageDependencies t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'PackageFrameworks'                    = "SELECT t.* FROM dbo.PackageFrameworks t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'PackageTypes'                         = "SELECT t.* FROM dbo.PackageTypes t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'PackageAuthors'                       = "SELECT t.* FROM dbo.PackageAuthors t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'PackageHistories'                     = "SELECT t.* FROM dbo.PackageHistories t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'SymbolPackages'                       = "SELECT t.* FROM dbo.SymbolPackages t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'PackageDeprecations'                  = "SELECT t.* FROM dbo.PackageDeprecations t JOIN #Pkg k ON t.PackageKey = k.[Key]"
        'PackageRenames'                       = "SELECT t.* FROM dbo.PackageRenames t JOIN #Reg a ON t.FromPackageRegistrationKey = a.[Key] JOIN #Reg b ON t.ToPackageRegistrationKey = b.[Key]"
        'UserSecurityPolicies'                 = "SELECT t.* FROM dbo.UserSecurityPolicies t JOIN #Usr k ON t.UserKey = k.[Key]"
        'UserCertificates'                     = "SELECT t.* FROM dbo.UserCertificates t JOIN #Usr u ON t.UserKey = u.[Key] JOIN #Cert c ON t.CertificateKey = c.[Key]"
        'Memberships'                          = "SELECT t.* FROM dbo.Memberships t JOIN #Usr o ON t.OrganizationKey = o.[Key] JOIN #Usr m ON t.MemberKey = m.[Key]"
        'PackageRegistrationOwners'            = "SELECT t.* FROM dbo.PackageRegistrationOwners t JOIN #Reg r ON t.PackageRegistrationKey = r.[Key] JOIN #Usr u ON t.UserKey = u.[Key]"
        'PackageRegistrationRequiredSigners'   = "SELECT t.* FROM dbo.PackageRegistrationRequiredSigners t JOIN #Reg r ON t.PackageRegistrationKey = r.[Key] JOIN #Usr u ON t.UserKey = u.[Key]"
        'UserRoles'                            = "SELECT t.* FROM dbo.UserRoles t JOIN #Usr u ON t.UserKey = u.[Key] JOIN #Role r ON t.RoleKey = r.[Key]"
        'ReservedNamespaceOwners'              = "SELECT t.* FROM dbo.ReservedNamespaceOwners t JOIN #Rns n ON t.ReservedNamespaceKey = n.[Key] JOIN #Usr u ON t.UserKey = u.[Key]"
        'ReservedNamespaceRegistrations'       = "SELECT t.* FROM dbo.ReservedNamespaceRegistrations t JOIN #Rns n ON t.ReservedNamespaceKey = n.[Key] JOIN #Reg r ON t.PackageRegistrationKey = r.[Key]"
        'VulnerablePackageVersionRangePackages'= "SELECT t.* FROM dbo.VulnerablePackageVersionRangePackages t JOIN #VRange v ON t.VulnerablePackageVersionRange_Key = v.[Key] JOIN #Pkg p ON t.Package_Key = p.[Key]"
    }

    # -----------------------------------------------------------------------
    # Write output
    # -----------------------------------------------------------------------
    if (-not (Test-Path -LiteralPath $OutputDirectory)) {
        New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    }
    $OutputDirectory = (Resolve-Path $OutputDirectory).Path
    Write-DsLog "Writing export to '$OutputDirectory'."

    $tableCounts = [ordered]@{}
    $tableSchemas = [ordered]@{}
    foreach ($table in $dumpQueries.Keys) {
        $rows = Invoke-DsQuery -Connection $connection -Query $dumpQueries[$table] -TimeoutSeconds $CommandTimeoutSeconds
        $tableCounts[$table] = $rows.Count
        if ($rows.Count -eq 0) { continue }

        # Capture column metadata (informational; import re-reads target schema).
        $columns = Get-DsTableColumns -Connection $connection -Table $table
        $tableSchemas[$table] = @($columns | ForEach-Object {
            [ordered]@{ name = $_.ColumnName; dataType = $_.DataType; isIdentity = [bool][int]$_.IsIdentity; isComputed = [bool][int]$_.IsComputed }
        })

        $encoded = @($rows | ForEach-Object { ConvertTo-DsJsonRow -Row $_ })
        Write-DsJsonFile -Path (Join-Path $OutputDirectory "$table.json") -InputObject $encoded
        Write-DsLog "  $table : $($rows.Count) row(s)."
    }

    $manifest = [ordered]@{
        formatVersion  = 1
        generatedUtc   = (Get-Date).ToUniversalTime().ToString('o')
        sourceDatabase = $databaseName
        adminsRoleKey  = if ($null -ne $adminsRoleKey) { [int]$adminsRoleKey } else { $null }
        criteria       = $criteria
        tableOrder     = @($dumpQueries.Keys)
        tableCounts    = $tableCounts
        tableSchemas   = $tableSchemas
    }
    Write-DsJsonFile -Path (Join-Path $OutputDirectory 'manifest.json') -InputObject $manifest

    $totalRows = ($tableCounts.Values | Measure-Object -Sum).Sum
    Write-DsLog "Export complete. $totalRows row(s) across $(@($tableCounts.Keys | Where-Object { $tableCounts[$_] -gt 0 }).Count) table(s)."
    Write-DsLog "Output folder: $OutputDirectory"
}
finally {
    $connection.Close()
    $connection.Dispose()
}
