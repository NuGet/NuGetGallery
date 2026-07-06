<#
.SYNOPSIS
    Shared helpers for the NuGetGallery data-sampling scripts
    (Export-SampleData.ps1, Import-SampleData.ps1, Copy-SamplePackages.ps1).

.DESCRIPTION
    Provides:
      * ADO.NET (System.Data.SqlClient) connection / query helpers.
      * Schema discovery (column list, identity / computed / rowversion flags).
      * JSON read / write helpers with type-preserving value encoding.
      * Value conversion between JSON primitives and typed SQL parameter values.
      * Simple leveled logging.

    The module deliberately has no external module dependencies (it does not
    require the SqlServer PowerShell module); it uses the System.Data.SqlClient
    types shipped with the .NET Framework / Windows PowerShell.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------

function Write-DsLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('INFO', 'WARN', 'ERROR', 'DEBUG')][string]$Level = 'INFO'
    )

    $timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $line = "[$timestamp] [$Level] $Message"

    switch ($Level) {
        'ERROR' { Write-Host $line -ForegroundColor Red }
        'WARN'  { Write-Host $line -ForegroundColor Yellow }
        'DEBUG' { Write-Verbose $line }
        default { Write-Host $line -ForegroundColor Gray }
    }
}

# ---------------------------------------------------------------------------
# SQL connection / commands
# ---------------------------------------------------------------------------

function New-DsSqlConnection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [switch]$ReadOnly
    )

    Add-Type -AssemblyName System.Data -ErrorAction SilentlyContinue | Out-Null

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)
    if ($ReadOnly) {
        # Hint for Availability Group read-only routing. Ignored by standalone servers.
        $builder.ApplicationIntent = [System.Data.SqlClient.ApplicationIntent]::ReadOnly
    }

    $connection = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    $connection.Open()
    return $connection
}

function New-DsCommand {
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$CommandText,
        [hashtable]$Parameters,
        $Transaction,
        [int]$TimeoutSeconds = 0
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $CommandText
    $command.CommandTimeout = $TimeoutSeconds
    if ($Transaction) { $command.Transaction = $Transaction }

    if ($Parameters) {
        foreach ($key in $Parameters.Keys) {
            $value = $Parameters[$key]
            if ($null -eq $value) { $value = [System.DBNull]::Value }
            [void]$command.Parameters.AddWithValue("@$key", $value)
        }
    }

    return $command
}

function Invoke-DsQuery {
    <#
        Executes a query and returns an array of [pscustomobject] rows with the
        native .NET values produced by the SqlDataReader (DBNull mapped to $null).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Query,
        [hashtable]$Parameters,
        $Transaction,
        [int]$TimeoutSeconds = 0
    )

    $command = New-DsCommand -Connection $Connection -CommandText $Query -Parameters $Parameters -Transaction $Transaction -TimeoutSeconds $TimeoutSeconds
    $reader = $command.ExecuteReader()
    $rows = New-Object System.Collections.Generic.List[object]
    try {
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $name = $reader.GetName($i)
                $value = $reader.GetValue($i)
                if ($value -is [System.DBNull]) { $value = $null }
                $row[$name] = $value
            }
            $rows.Add([pscustomobject]$row)
        }
    }
    finally {
        $reader.Close()
        $command.Dispose()
    }

    return , $rows.ToArray()
}

function Invoke-DsScalar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Query,
        [hashtable]$Parameters,
        $Transaction,
        [int]$TimeoutSeconds = 0
    )

    $command = New-DsCommand -Connection $Connection -CommandText $Query -Parameters $Parameters -Transaction $Transaction -TimeoutSeconds $TimeoutSeconds
    try {
        $result = $command.ExecuteScalar()
        if ($result -is [System.DBNull]) { return $null }
        return $result
    }
    finally {
        $command.Dispose()
    }
}

function Invoke-DsNonQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Query,
        [hashtable]$Parameters,
        $Transaction,
        [int]$TimeoutSeconds = 0
    )

    $command = New-DsCommand -Connection $Connection -CommandText $Query -Parameters $Parameters -Transaction $Transaction -TimeoutSeconds $TimeoutSeconds
    try {
        return $command.ExecuteNonQuery()
    }
    finally {
        $command.Dispose()
    }
}

# ---------------------------------------------------------------------------
# Schema discovery
# ---------------------------------------------------------------------------

function Get-DsTableColumns {
    <#
        Returns column metadata for a table, ordered by column id. Each row has:
        ColumnName, DataType (SQL type name), IsIdentity, IsComputed, IsNullable, MaxLength.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Table,
        [string]$Schema = 'dbo',
        $Transaction
    )

    $query = @'
SELECT
    c.name              AS ColumnName,
    t.name              AS DataType,
    c.is_identity       AS IsIdentity,
    c.is_computed       AS IsComputed,
    c.is_nullable       AS IsNullable,
    c.max_length        AS MaxLength,
    c.column_id         AS Ordinal
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(@tbl)
ORDER BY c.column_id
'@

    return Invoke-DsQuery -Connection $Connection -Query $query -Parameters @{ tbl = "$Schema.$Table" } -Transaction $Transaction
}

function Test-DsTableExists {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Table,
        [string]$Schema = 'dbo',
        $Transaction
    )

    $result = Invoke-DsScalar -Connection $Connection -Query 'SELECT OBJECT_ID(@tbl)' -Parameters @{ tbl = "$Schema.$Table" } -Transaction $Transaction
    return $null -ne $result
}

# Columns that must never be supplied on INSERT because the server generates them.
function Test-DsServerGeneratedColumn {
    param([Parameter(Mandatory)]$Column)
    return ([int]$Column.IsIdentity -eq 1) -or
           ([int]$Column.IsComputed -eq 1) -or
           ($Column.DataType -in @('timestamp', 'rowversion'))
}

# ---------------------------------------------------------------------------
# JSON value encoding / decoding
# ---------------------------------------------------------------------------

function ConvertTo-DsJsonValue {
    <#
        Converts a native value read from SQL into a JSON-friendly primitive.
        DateTime/DateTimeOffset -> round-trip ISO 8601 string ("o").
        byte[]                  -> base64 string.
        Guid                    -> string.
        Everything else is returned unchanged.
    #>
    param($Value)

    if ($null -eq $Value) { return $null }
    if ($Value -is [datetime]) { return $Value.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture) }
    if ($Value -is [datetimeoffset]) { return $Value.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture) }
    if ($Value -is [byte[]]) { return [System.Convert]::ToBase64String($Value) }
    if ($Value -is [guid]) { return $Value.ToString() }
    return $Value
}

function ConvertTo-DsJsonRow {
    <# Encodes a [pscustomobject] DB row into an ordered hashtable of JSON-friendly values. #>
    param([Parameter(Mandatory)]$Row)

    $encoded = [ordered]@{}
    foreach ($property in $Row.PSObject.Properties) {
        $encoded[$property.Name] = ConvertTo-DsJsonValue -Value $property.Value
    }
    return $encoded
}

function ConvertTo-DsSqlValue {
    <#
        Converts a JSON primitive back into a typed value suitable for a
        SqlParameter, based on the destination column's SQL data type.
    #>
    param(
        $Value,
        [Parameter(Mandatory)][string]$DataType
    )

    if ($null -eq $Value) { return [System.DBNull]::Value }

    $invariant = [System.Globalization.CultureInfo]::InvariantCulture
    $roundtrip = [System.Globalization.DateTimeStyles]::RoundtripKind

    # ConvertFrom-Json may already have materialized typed values (e.g. ISO 8601
    # strings become [datetime]). Pass those straight through so we never lose
    # precision via a culture-sensitive string round-trip.
    if ($Value -is [datetime])       { return $Value }
    if ($Value -is [datetimeoffset]) { return $Value }
    if ($Value -is [timespan])       { return $Value }

    switch -Regex ($DataType.ToLowerInvariant()) {
        '^datetimeoffset$'                  { return [datetimeoffset]::Parse([string]$Value, $invariant, $roundtrip) }
        '^(datetime2|datetime|smalldatetime|date)$' { return [datetime]::Parse([string]$Value, $invariant, $roundtrip) }
        '^time$'                            { return [timespan]::Parse([string]$Value, $invariant) }
        '^(binary|varbinary|image)$'        { return , ([System.Convert]::FromBase64String([string]$Value)) }
        '^uniqueidentifier$'                { return [guid]::Parse([string]$Value) }
        '^bit$'                             { return [bool]$Value }
        '^bigint$'                          { return [long]$Value }
        '^(int|smallint|tinyint)$'          { return [int]$Value }
        '^(decimal|numeric|money|smallmoney)$' { return [decimal]$Value }
        '^(float|real)$'                    { return [double]$Value }
        default                             { return [string]$Value }
    }
}

function Write-DsJsonFile {
    <# Serializes an object graph to UTF-8 (no BOM) JSON. #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$InputObject,
        [int]$Depth = 100
    )

    $json = $InputObject | ConvertTo-Json -Depth $Depth
    if ($null -eq $json) { $json = '[]' }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function Read-DsJsonFile {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }
    $text = [System.IO.File]::ReadAllText($Path)
    if ([string]::IsNullOrWhiteSpace($text)) { return @() }
    return $text | ConvertFrom-Json
}

# ---------------------------------------------------------------------------
# Input file helpers
# ---------------------------------------------------------------------------

function Read-DsLineList {
    <# Reads a UTF-8 text file, one value per line, trimming blanks and comments (#). #>
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Input list file not found: $Path"
    }

    $lines = [System.IO.File]::ReadAllLines($Path, [System.Text.Encoding]::UTF8)
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrEmpty($trimmed)) { continue }
        if ($trimmed.StartsWith('#')) { continue }
        $result.Add($trimmed)
    }
    return , $result.ToArray()
}

# ---------------------------------------------------------------------------
# Temp-table helper for IN (...) style filtering against large lists.
# ---------------------------------------------------------------------------

function New-DsStringTempTable {
    <#
        Creates a session #temp table with a single nvarchar Value column and
        populates it with the supplied values using a parameterized batch insert.
        Returns the temp table name (e.g. '#DsFilter').
        Works against read-only databases / read-only AG replicas because the
        temp table lives in tempdb.
    #>
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Values,
        [string]$TableName = '#DsFilter',
        [int]$MaxLength = 256,
        $Transaction
    )

    Invoke-DsNonQuery -Connection $Connection -Transaction $Transaction -Query "IF OBJECT_ID('tempdb..$TableName') IS NOT NULL DROP TABLE $TableName; CREATE TABLE $TableName (Value NVARCHAR($MaxLength) COLLATE DATABASE_DEFAULT NOT NULL);" | Out-Null

    if ($Values.Count -eq 0) { return $TableName }

    # Insert in batches of up to 1000 rows (SQL Server row-value-constructor limit).
    $batchSize = 1000
    for ($start = 0; $start -lt $Values.Count; $start += $batchSize) {
        $end = [Math]::Min($start + $batchSize, $Values.Count) - 1
        $rowClauses = New-Object System.Collections.Generic.List[string]
        $parameters = @{}
        for ($i = $start; $i -le $end; $i++) {
            $param = "v$i"
            $rowClauses.Add("(@$param)")
            $parameters[$param] = $Values[$i]
        }
        $sql = "INSERT INTO $TableName (Value) VALUES " + ($rowClauses -join ',')
        Invoke-DsNonQuery -Connection $Connection -Transaction $Transaction -Query $sql -Parameters $parameters | Out-Null
    }

    return $TableName
}

function Test-DsNaturalKeyClash {
    <#
        Fails (throws) if any of the supplied rows' natural-key values already exist
        in the destination table. Used by import to enforce the "target is empty"
        assumption with a clear error message instead of a raw unique-constraint error.
    #>
    param(
        [Parameter(Mandatory)]$Connection,
        [Parameter(Mandatory)][string]$Table,
        [Parameter(Mandatory)][string]$Column,
        [Parameter(Mandatory)]$Rows,
        $Transaction,
        [string]$Schema = 'dbo'
    )

    $values = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($row in $Rows) {
        if (-not $row.PSObject.Properties[$Column]) { continue }
        $value = $row.$Column
        if ($null -eq $value) { continue }
        $text = [string]$value
        if ($seen.Add($text)) { $values.Add($text) }
    }
    if ($values.Count -eq 0) { return }

    $tempName = '#DsNatCheck'
    [void](New-DsStringTempTable -Connection $Connection -Transaction $Transaction -Values $values.ToArray() -TableName $tempName -MaxLength 450)

    $query = @"
SELECT TOP 5 f.Value
FROM $tempName f
JOIN $Schema.[$Table] t ON CAST(t.[$Column] AS NVARCHAR(450)) = f.Value
"@
    $clashes = Invoke-DsQuery -Connection $Connection -Transaction $Transaction -Query $query
    Invoke-DsNonQuery -Connection $Connection -Transaction $Transaction -Query "IF OBJECT_ID('tempdb..$tempName') IS NOT NULL DROP TABLE $tempName" | Out-Null

    if ($clashes.Count -gt 0) {
        $sample = ($clashes | ForEach-Object { $_.Value }) -join ', '
        throw "Natural-key collision in dbo.$Table on column '$Column': value(s) already present in the target database (e.g. $sample). The target is expected to be empty for these rows."
    }
}

Export-ModuleMember -Function `
    Write-DsLog, `
    New-DsSqlConnection, New-DsCommand, Invoke-DsQuery, Invoke-DsScalar, Invoke-DsNonQuery, `
    Get-DsTableColumns, Test-DsTableExists, Test-DsServerGeneratedColumn, `
    ConvertTo-DsJsonValue, ConvertTo-DsJsonRow, ConvertTo-DsSqlValue, Write-DsJsonFile, Read-DsJsonFile, `
    Read-DsLineList, New-DsStringTempTable, Test-DsNaturalKeyClash
