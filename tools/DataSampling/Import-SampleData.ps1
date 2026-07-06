<#
.SYNOPSIS
    Imports a JSON export produced by Export-SampleData.ps1 into a (mostly empty)
    NuGet Gallery database with the same schema, regenerating identity keys while
    preserving all relationships.

.DESCRIPTION
    Inserts rows in a FK-safe order (see DataSampling.Schema.psm1). For every table
    that owns an identity primary key, the original key is mapped to a freshly
    generated key (captured via SCOPE_IDENTITY); foreign-key columns are then
    rewritten to point at the new parent keys. The TPT 'Organizations' subtype and
    all join tables are remapped the same way.

    Server-generated columns (identity keys, computed columns, and rowversion/timestamp
    columns such as SymbolPackages.RowVersion) are never written. All other columns,
    including timestamp columns with GETUTCDATE() defaults (Created, LastUpdated,
    Published, DeprecatedOn, UpdatedOn), are inserted verbatim to preserve fidelity.

    The 'Packages' table has an AFTER INSERT/UPDATE trigger (LastEditedTrigger) that
    overwrites a non-null LastEdited on insert. With -PreserveLastEdited (default), the
    trigger is temporarily disabled around the Packages insert so original values survive.

    The target is assumed to be otherwise empty: a natural-key collision (e.g. an existing
    Username, PackageRegistration.Id, Role.Name, Certificate.Thumbprint) fails the import.

    All work runs in a single transaction (use -NoTransaction to disable).

.EXAMPLE
    .\Import-SampleData.ps1 -ConnectionString "Server=.;Database=NuGetGallery_Test;Integrated Security=SSPI" `
        -InputDirectory .\out\export-20240101-120000
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ConnectionString,
    [Parameter(Mandatory)][string]$InputDirectory,

    [bool]$PreserveLastEdited = $true,
    [switch]$NoTransaction,
    [int]$CommandTimeoutSeconds = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'DataSampling.Common.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'DataSampling.Schema.psm1') -Force

if (-not (Test-Path -LiteralPath $InputDirectory)) {
    throw "Input directory not found: $InputDirectory"
}
$InputDirectory = (Resolve-Path $InputDirectory).Path

$manifestPath = Join-Path $InputDirectory 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in $InputDirectory"
}
$manifest = Read-DsJsonFile -Path $manifestPath
Write-DsLog "Loaded manifest (source database '$($manifest.sourceDatabase)', generated $($manifest.generatedUtc))."

$schemaModel = Get-DsSchemaModel

# old-key -> new-key maps, indexed by map name (usually the parent table name).
$keyMaps = @{}
foreach ($entry in $schemaModel) {
    if ($entry.KeyMap) { $keyMaps[$entry.KeyMap] = @{} }
}

function Resolve-DsForeignKey {
    param(
        [Parameter(Mandatory)][hashtable]$KeyMaps,
        [Parameter(Mandatory)][string]$MapName,
        $OldValue,
        [Parameter(Mandatory)][string]$Table,
        [Parameter(Mandatory)][string]$Column
    )

    if ($null -eq $OldValue) { return [System.DBNull]::Value }
    $old = [int]$OldValue
    if (-not $KeyMaps[$MapName].ContainsKey($old)) {
        throw "Referential integrity error: $Table.$Column references $MapName key $old which was not imported. The export is incomplete."
    }
    return [int]$KeyMaps[$MapName][$old]
}

$connection = New-DsSqlConnection -ConnectionString $ConnectionString
$transaction = $null
try {
    if (-not $NoTransaction) {
        $transaction = $connection.BeginTransaction()
        Write-DsLog "Started transaction."
    }

    foreach ($entry in $schemaModel) {
        $table = $entry.Table
        $jsonPath = Join-Path $InputDirectory "$table.json"
        if (-not (Test-Path -LiteralPath $jsonPath)) {
            Write-DsLog "Skipping $table (no data file)." 'DEBUG'
            continue
        }

        $rows = @(Read-DsJsonFile -Path $jsonPath)
        if ($rows.Count -eq 0) {
            Write-DsLog "Skipping $table (empty)." 'DEBUG'
            continue
        }

        if (-not (Test-DsTableExists -Connection $connection -Table $table -Transaction $transaction)) {
            throw "Target table dbo.$table does not exist."
        }

        # Destination column metadata.
        $columns = Get-DsTableColumns -Connection $connection -Table $table -Transaction $transaction
        $typeMap = @{}
        $insertable = New-Object System.Collections.Generic.List[string]
        foreach ($column in $columns) {
            $typeMap[$column.ColumnName] = $column.DataType
            if (-not (Test-DsServerGeneratedColumn -Column $column)) {
                $insertable.Add($column.ColumnName)
            }
        }

        $identityKey = $entry.IdentityKey
        $fks = $entry.Fks
        $mapForThisTable = if ($entry.KeyMap) { $keyMaps[$entry.KeyMap] } else { $null }

        # Fail-fast natural-key collision check.
        if ($entry.NaturalKey) {
            Test-DsNaturalKeyClash -Connection $connection -Transaction $transaction -Table $table -Column $entry.NaturalKey -Rows $rows
        }

        # Disable the LastEdited trigger around Packages so original values survive.
        $triggerDisabled = $false
        if ($table -eq 'Packages' -and $PreserveLastEdited) {
            $triggerExists = Invoke-DsScalar -Connection $connection -Transaction $transaction -Query "SELECT OBJECT_ID('dbo.LastEditedTrigger')"
            if ($null -ne $triggerExists) {
                Invoke-DsNonQuery -Connection $connection -Transaction $transaction -Query "DISABLE TRIGGER [dbo].[LastEditedTrigger] ON [dbo].[Packages]" | Out-Null
                $triggerDisabled = $true
                Write-DsLog "Temporarily disabled LastEditedTrigger to preserve Package.LastEdited."
            }
        }

        try {
            $insertList = $insertable.ToArray()
            $columnSql = ($insertList | ForEach-Object { "[$_]" }) -join ', '
            $paramSql = (0..($insertList.Count - 1) | ForEach-Object { "@p$_" }) -join ', '

            $baseInsert = "INSERT INTO dbo.[$table] ($columnSql) VALUES ($paramSql)"
            $insertWithIdentity = "$baseInsert; SELECT CAST(SCOPE_IDENTITY() AS BIGINT);"

            $rowNumber = 0
            foreach ($row in $rows) {
                $rowNumber++
                $parameters = @{}
                for ($i = 0; $i -lt $insertList.Count; $i++) {
                    $col = $insertList[$i]
                    $rawValue = if ($row.PSObject.Properties[$col]) { $row.$col } else { $null }

                    if ($fks.ContainsKey($col)) {
                        $value = Resolve-DsForeignKey -KeyMaps $keyMaps -MapName $fks[$col] -OldValue $rawValue -Table $table -Column $col
                    }
                    else {
                        $value = ConvertTo-DsSqlValue -Value $rawValue -DataType $typeMap[$col]
                    }
                    $parameters["p$i"] = $value
                }

                if ($identityKey) {
                    $newKey = Invoke-DsScalar -Connection $connection -Transaction $transaction -Query $insertWithIdentity -Parameters $parameters -TimeoutSeconds $CommandTimeoutSeconds
                    $oldKey = [int]$row.$identityKey
                    $mapForThisTable[$oldKey] = [int]$newKey
                }
                else {
                    Invoke-DsNonQuery -Connection $connection -Transaction $transaction -Query $baseInsert -Parameters $parameters -TimeoutSeconds $CommandTimeoutSeconds | Out-Null
                }
            }

            Write-DsLog "Imported $table : $($rows.Count) row(s)."
        }
        finally {
            if ($triggerDisabled) {
                Invoke-DsNonQuery -Connection $connection -Transaction $transaction -Query "ENABLE TRIGGER [dbo].[LastEditedTrigger] ON [dbo].[Packages]" | Out-Null
                Write-DsLog "Re-enabled LastEditedTrigger."
            }
        }
    }

    if ($transaction) {
        $transaction.Commit()
        Write-DsLog "Committed transaction."
    }
    Write-DsLog "Import complete."
}
catch {
    if ($transaction) {
        try { $transaction.Rollback(); Write-DsLog "Rolled back transaction." 'WARN' } catch { }
    }
    throw
}
finally {
    if ($transaction) { $transaction.Dispose() }
    $connection.Close()
    $connection.Dispose()
}
