# Search auxiliary files

**Subsystem: Search 🔎**

Aside from metadata stored in the [Azure Search indexes](Azure-Search-indexes.md), there is data stored in Azure Blob
Storage for bookkeeping and performance reasons. These data files are called **auxiliary files**. The data files
mentioned here are those explicitly managed by the search subsystem. Other data files exist (manually created,
created by the statistics subsystem, etc.). Those will not be covered here but are mentioned in the job-specific
documentation that uses them as input.

Each search auxiliary file is copied to the individual region that a [search service](../src/NuGet.Services.SearchService/README.md)
is deployed. For nuget.org, we run search in four regions, so there are four copies of each of these files.

The search auxiliary files are:

  - [`downloads/downloads.v2.json`](#download-count-data) - total download count for every package version
  - [`owners/owners.v2.json` and change history](#package-ownership-data) - owners for every package ID
  - [`verified-packages/verified-packages.v1.json`](#verified-packages-data) - package IDs that are verified
  - [`popularity-transfers/popularity-transfers.v1.json`](#popularity-transfer-data) - popularity transfers between package IDs

There is an additional auxiliary file that is *not* copied to the region-specific storage container. It is only used
during the index rebuild process by [Db2AzureSearch](../src/NuGet.Jobs.Db2AzureSearch).

  - [`ExcludedPackages.v1.json`](#excluded-packages) - package IDs excluded from the default search results

## Download count data

The `downloads/downloads.v2.json` file has the total download count for all package versions. The total download count
for a package ID as a whole can be calculated simply by adding all version download counts.

The downloads data file looks like this:

```json
{
  "Newtonsoft.Json": {
    "8.0.3": 10508321,
    "9.0.1": 55801938
  },
  "NuGet.Versioning": {
    "5.6.0-preview.3.6558": 988,
    "5.6.0": 10224
  }
}
```

The package ID and version keys are not guaranteed to have the original (author-intended) casing and should be treated
in a case insensitive manner. The version keys will always be normalized via [standard `NuGetVersion` normalization rules](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning#normalized-version-numbers)
(e.g. no build metadata will appear, no leading zeroes, etc.).

If a package ID or version does not exist in the data file, this only indicates that there is no download count data and
does not imply that the package ID or version does not exist on the package source. It is possible for package IDs or
versions that do not exist (perhaps due to deletion) to exist in the data file. 

The order of the IDs and versions in the file is undefined.

This file has a "v2" in the file name because it is the second version of this data. The "v1" format is still produced
by the statistics subsystem and has a less friendly data format.

The class for reading and writing this file to Blob Storage is [`DownloadDataClient`](../src/NuGet.Services.AzureSearch/AuxiliaryFiles/DownloadDataClient.cs).

## Package ownership data

The `owners/owners.v2.json` file contains the owner information about all package IDs. Each time this file is updated,
the set of package IDs that changed is written to a "change history" file with a path pattern like
`owners/changes/TIMESTAMP.json`.

The class for reading and writing these files to Blob Storage is [`OwnerDataClient`](../src/NuGet.Services.AzureSearch/AuxiliaryFiles/OwnerDataClient.cs).

### `owners/owners.v2.json`

The owners data file looks like this:

```json
{
  "Newtonsoft.Json": [
    "dotnetfoundation",
    "jamesnk",
    "newtonsoft"
  ],
  "NuGet.Versioning": [
    "Microsoft",
    "nuget"
  ]
}
```

The package ID key is not guaranteed to have the original (author-intended) casing and should be treated
in a case insensitive manner. The owner values will have the same casing that is shown on NuGetGallery but should be
treated in a case insensitive manner.

If a package ID does not exist in the data file, this indicates that the package ID has no owners (a possible but
relatively rare scenario for NuGetGallery). It is possible for a package ID with no versions to appear in this file.

The order of the IDs and owner usernames in the file is case insensitive ascending lexicographical order.

This file has a "v2" in the file name because it is the second version of this data. The "v1" format was deprecated when
nuget.org moved from a Lucene-based search service to Azure Search. The "v1" format had a less friendly data format.

### Change history

The change history files do not contain owner usernames for GDPR reasons but mention all of the package IDs that had
ownership changes since the last time that the `owners.v2.json` file was generated. If a package ID is not mentioned in
a file, that means that there were no ownership changes in the time window. An ownership change is defined as one or
more owners being added or removed from the set of owners for that package ID.

Each change history data file has a file name with timestamp format `yyyy-MM-dd-HH-mm-ss-FFFFFFF` (UTC) and a file
extension of `.json`.

The files look like this:

```json
[
  "Newtonsoft.Json",
  "NuGet.Versioning"
]
```

By processing the files in order of their timestamp file name, a rough log of ownership changes can be produced. These
files are currently not read by any job and are produced for future investigative purposes.

The package ID key is not guaranteed to have the original (author-intended) casing and should be treated
in a case insensitive manner.

The order of the package IDs in the file is undefined.

## Verified packages data

The `verified-packages/verified-packages.v1.json` data file contains all package IDs that are considered verified by the [prefix reservation feature](https://docs.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation). This essentially defines the verified checkmark icon in the search UIs.

The data file looks like this:

```json
[
  "Newtonsoft.Json",
  "NuGet.Versioning"
]
```

If a package ID is in the file, then it is verified. The package ID is not guaranteed to have the original
(author-intended) casing and should be treated in a case insensitive manner. 

The order of the package IDs is undefined.

The class for reading and writing this file to Blob Storage is [`VerifiedPackagesDataClient`](../src/NuGet.Services.AzureSearch/AuxiliaryFiles/VerifiedPackagesDataClient.cs).

## Popularity transfer data

The `popularity-transfers/popularity-transfers.v1.json` data file has a mapping of all package IDs that have
transferred their popularity to one or more other packages.

The data file looks like this:

```json
{
  "OldPackageA": [
    "NewPackage1",
    "NewPackage2"
  ],
  "OldPackageB": [
    "NewPackage3"
  ]
}
```

For each key-value pair, the package ID key has its popularity transferred to the package ID values. The implementation
of the popularity transfer is out of scope for the data file format. Package IDs that do not appear as a key in this
file do not have their popularity transferred.

The package ID keys and values are not guaranteed to have the original (author-intended) casing and should be treated
in a case insensitive manner.

The order of the package ID keys and values is case insensitive ascending lexicographical order.

The class for reading and writing this file to Blob Storage is [`PopularityTransferDataClient`](../src/NuGet.Services.AzureSearch/AuxiliaryFiles/PopularityTransferDataClient.cs).

## Excluded packages

The `ExcludedPackages.v1.json` file is not present in the region-specific storage accounts and is only used during the
index rebuild process. It contains a list of package IDs that should be excluded from the default search. Default search
is the search query that has no search text at all (empty search text). It is displayed on NuGet.org when you click the
"Packages" link in the navigation tab and in the Visual Studio Package Manager UI, on the Browse tab.

This file is used to prevent the default search results from being filled with somewhat uninteresting, high download
packages that ship as part of the .NET BCL. These packages are rarely installed manually so it's not useful to show them
in the default search results.

Note that this is *not* the same as unlisting the package. An unlisted package does not appear in any searches against
the [Search index](Azure-Search-indexes.md#search-index). Package IDs mentioned in the excluded packages list do appear
in any search that has some search text (e.g. searching for that specific excluded package ID).

The data file looks like this:

```json
[
  "Microsoft.Extensions.Primitives",
  "Microsoft.NETCore.Platforms",
  "Microsoft.Extensions.DependencyInjection.Abstractions",
  "System.Runtime.CompilerServices.Unsafe"
]
```

Note that the "excluded packages" list that is used by 
[Db2AzureSearch](../src/NuGet.Jobs.Db2AzureSearch) is currently not updated by
[Auxiliary2AzureSearch](../src/NuGet.Jobs.Auxiliary2AzureSearch).
This is an acceptable limitation because this list is manually updated by the NuGet.org team and doesn't change
frequently. The index rebuild process must be performed whenever the list changes. This limitation is tracked by
[NuGet/NuGetGallery#7384](https://github.com/NuGet/NuGetGallery/issues/7384).
