# NuGet Gallery Data Sampling Tools

A set of PowerShell scripts that **sample** a subset of NuGet Gallery package data from a
**read-only** SQL Server database, persist it to relationship-preserving JSON, **import** it into a
mostly-empty database with the same schema (regenerating identity keys), and **copy** the matching
`.nupkg` blobs between Azure Blob Storage accounts.

```
Export-SampleData.ps1   read-only SQL DB  ->  JSON export folder
Import-SampleData.ps1   JSON export folder ->  empty SQL DB (new keys, same relationships)
Copy-SamplePackages.ps1 JSON export folder ->  azcopy .nupkg blobs (source -> destination)
```

| File | Purpose |
|------|---------|
| `Export-SampleData.ps1` | Select packages by 3 rules, walk the relationship graph, dump JSON. |
| `Import-SampleData.ps1` | Insert the JSON into a same-schema DB, remapping keys and preserving relations. |
| `Copy-SamplePackages.ps1` | Compute blob names from the export and `azcopy` the `.nupkg` files. |
| `DataSampling.Common.psm1` | Shared helpers: ADO.NET, schema discovery, JSON value encode/decode, logging. |
| `DataSampling.Schema.psm1` | Single source of truth for table insert order + FK remap metadata. |

## Prerequisites

- **PowerShell** 5.1+ or PowerShell 7+ (Windows). Uses `System.Data.SqlClient` (bundled on Windows
  PowerShell; available via the `SqlServer`/`Microsoft.Data.SqlClient` stack on PS7 — no extra module
  is required because the scripts use ADO.NET directly).
- **SQL Server** access to the source (read-only is fine, including AG readable secondaries — temp
  tables are used for IN-list filtering so the source is never written to) and to the destination DB.
- **azcopy** v10+ on `PATH` (or pass `-AzCopyPath`) — only needed for `Copy-SamplePackages.ps1`.
- Permission on the destination storage account (Azure AD). Run `azcopy login` first, or pass
  `-AutoLoginType` (sets `AZCOPY_AUTO_LOGIN_TYPE`).

## 1. Export — `Export-SampleData.ps1`

Selects a **root set** of packages via three independently-evaluated rules (their union, de-duplicated),
then closes over all **owned / outbound** relationships and writes one JSON file per table plus a
`manifest.json`.

### Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `-ConnectionString` | yes | ADO.NET connection string to the **source** DB. |
| `-PackageIdsFile` | rule 1 | UTF-8 text file, one `PackageRegistration.Id` per line. |
| `-OwnerUsernamesFile` | rule 2 | UTF-8 text file, one owner `Username` per line (user **or** organization). |
| `-SamplePercentage` | rule 3 | `0`–`100`. Percentage of registrations that have ≥1 listed version. |
| `-Seed` | no | Integer seed for **reproducible** percentage sampling (default: non-deterministic). |
| `-OutputDirectory` | no | Export folder (default `out\export-<timestamp>\`). |
| `-CommandTimeoutSeconds` | no | SQL command timeout (`0` = no timeout). |

At least one rule must be supplied.

### Selection rules

- **Rule 1 — Package IDs:** `PackageRegistration.Id IN (file)`. Exports every version where
  `PackageStatusKey <> 1` (not Deleted).
- **Rule 2 — Owners:** resolve `Users.Username IN (file)` (organizations also live in `Users`, with an
  extra `Organizations` row). Exports all not-deleted versions of registrations they own
  (`PackageRegistrationOwners`).
- **Rule 3 — Percentage:** from registrations having ≥1 `Package.Listed = 1`, randomly sample the given
  percentage of Ids and export their `Listed = 1 AND PackageStatusKey <> 1` versions.

### Relationship traversal

Starting from the selected registrations/packages, the export iterates to a **fixpoint**, following
only **outbound / owned** references (never dragging in unrelated siblings via inverse navigations):

- **Package** → its registration, uploader `User` (`UserKey`), `Certificate` (`CertificateKey`),
  `PackageDependencies`, `PackageTypes`, `PackageFrameworks`, `PackageAuthors`, `PackageHistories`
  (+ their users), `SymbolPackages`, `PackageDeprecations` (→ alternate `Package` **and** its
  registration, alternate registration, deprecating user), and `VulnerablePackageVersionRangePackages`
  → ranges → `PackageVulnerabilities`.
- **PackageRegistration** → owners, required signers, reserved-namespace registrations (→ namespaces →
  their owners), and package renames (both `From`/`To` registrations, iterated to closure).
- **User** → `Organizations` row (if an org), `Memberships` (both endpoints), `UserRoles` → `Roles`,
  `UserCertificates` → `Certificates`, `UserSecurityPolicies`.

### Hard exclusions (never exported, even if linked)

- **Secrets:** `Credentials`, `Scopes`, `FederatedCredentials`, `FederatedCredentialPolicies`.
- **Admins role:** the `Roles` row with `Name = 'Admins'` (its `Key` is recorded in the manifest) and
  **all** `UserRoles` rows referencing that key.

### Examples

```powershell
# Rule 1 + Rule 2
.\Export-SampleData.ps1 `
    -ConnectionString "Server=tcp:mydb.database.windows.net;Database=Gallery;Authentication=Active Directory Default;" `
    -PackageIdsFile .\ids.txt `
    -OwnerUsernamesFile .\owners.txt

# Rule 3, reproducible 5% sample
.\Export-SampleData.ps1 -ConnectionString $cs -SamplePercentage 5 -Seed 12345
```

## 2. Import — `Import-SampleData.ps1`

Inserts the export into a same-schema DB in FK-safe order, capturing `SCOPE_IDENTITY()` into per-table
`oldKey → newKey` maps and remapping every foreign key. The target is assumed otherwise empty — the
script **fails fast** (and rolls back) on any natural-key collision (e.g. `Username`,
`PackageRegistration.Id`, `Role.Name`, `Certificate.Thumbprint`).

### Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `-ConnectionString` | yes | ADO.NET connection string to the **destination** DB. |
| `-InputDirectory` | yes | Export folder produced by `Export-SampleData.ps1`. |
| `-PreserveLastEdited` | no | Default `$true`. Temporarily disables `[dbo].[LastEditedTrigger]` around the `Packages` insert so the original `Package.LastEdited` survives. |
| `-NoTransaction` | no | Insert without a single wrapping transaction (default: one transaction, rolled back on error). |
| `-CommandTimeoutSeconds` | no | SQL command timeout (`0` = no timeout). |

### Key fidelity notes

- **Identity `Key` columns** are *not* inserted — they are regenerated and remapped (the whole point).
- **Timestamp columns** (`Created`, `LastUpdated`, `Published`, `DeprecatedOn`, `UpdatedOn`, …) **are**
  inserted verbatim; in EF they carry `[DatabaseGenerated(Computed)]` but in SQL they are plain
  `DateTime` columns with `GETUTCDATE()` *defaults*, so explicit inserts are accepted and preserved.
- **`SymbolPackage.RowVersion`** is a SQL `rowversion`/`timestamp` and is the only non-identity column
  that is omitted (regenerated by the server).
- Organizations use TPT: the base `Users` row is inserted first, then the `Organizations` row keyed by
  the new `User` key.

### Example

```powershell
.\Import-SampleData.ps1 `
    -ConnectionString "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Database=GalleryTest;" `
    -InputDirectory .\out\export-20240101-120000
```

## 3. Copy blobs — `Copy-SamplePackages.ps1`

Reads `Packages.json` (+ `PackageRegistrations.json` to resolve Ids), computes each blob name as
`{id-lower}.{normalizedVersion-lower}.nupkg` (all blobs live at the container root), writes an
`azcopy-list.txt`, ensures the destination container exists (`azcopy make`, same name as the source),
and runs a single batched `azcopy copy --list-of-files`.

### Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `-InputDirectory` | yes | Export folder. |
| `-SourceAccount` / `-SourceContainer` | yes | Source storage account + container (assumed anonymous/public read). |
| `-DestinationAccount` | yes | Destination storage account. |
| `-DestinationContainer` | no | Defaults to the **source** container name (path preserved). |
| `-EndpointSuffix` | no | Default `core.windows.net`. |
| `-SourceContainerUrl` / `-DestinationContainerUrl` | no | Explicit URL overrides (e.g. to embed a SAS); take precedence over account/container. |
| `-AutoLoginType` | no | `AZCLI` \| `MSI` \| `DEVICE` \| `PSCRED` \| `WORKLOAD` — sets `AZCOPY_AUTO_LOGIN_TYPE` for the destination. |
| `-AzCopyPath` | no | Path to `azcopy` (default: `azcopy` on `PATH`). |
| `-Overwrite` | no | `true` \| `false` \| `prompt` \| `ifSourceNewer` (default). |
| `-DryRun` | no | Print/azcopy `--dry-run`; transfers nothing. |

### Example

```powershell
azcopy login                       # or pass -AutoLoginType AZCLI after 'az login'
.\Copy-SamplePackages.ps1 `
    -InputDirectory .\out\export-20240101-120000 `
    -SourceAccount nugetprod -SourceContainer packages `
    -DestinationAccount mytestgallery -AutoLoginType AZCLI
```

## End-to-end workflow

```powershell
# 1. Sample from the production (read-only) DB
.\Export-SampleData.ps1 -ConnectionString $srcCs -PackageIdsFile .\ids.txt -OutputDirectory .\out\sample

# 2. Load into an empty test DB
.\Import-SampleData.ps1 -ConnectionString $dstCs -InputDirectory .\out\sample

# 3. Copy the package blobs
.\Copy-SamplePackages.ps1 -InputDirectory .\out\sample `
    -SourceAccount nugetprod -SourceContainer packages -DestinationAccount mytestgallery -AutoLoginType AZCLI
```

## Notes

- The export keeps original key values in the JSON **only as correlation handles**; they are never
  reused on import.
- The relationship closure is iterated until no new keys are discovered, so deprecation alternates and
  rename partners pull in their registrations automatically.
- Binary columns are stored as base64; `datetime`/`datetimeoffset` values are stored as ISO-8601
  round-trip strings (full sub-second precision preserved).
- The source DB is only ever read; temp tables (in `tempdb`) are used for large IN-list filtering, so
  the scripts work against read-only databases and readable AG secondaries.
