# Search version list resource

**Subsystem: Search 🔎**

The version list resource is bookkeeping used to update the search subsytem. Effectively, this resource is a mapping of package IDs to their versions. It is initially created by the [Db2AzureSearch](../src/NuGet.Jobs.Db2AzureSearch) tool and is kept up-to-date by the [Catalog2AzureSearch](../src/NuGet.Jobs.Catalog2AzureSearch) job.

## Purpose

The [search index](./Azure-Search-indexes.md#Search-index) stores up to 4 documents per package ID. These documents store the metadata for the latest listed version across these pivots:

1. Prerelease - Includes packages that aren't stable
1. SemVer - Includes packages that require SemVer 2.0.0 support

When packages are created, modified, or deleted we must decide which Azure Search documents must be updated. Furthermore, we need to decide which package version is the latest listed version across both prerelease and semver pivots. How do we do this? Using the search version list resource!

## Content

Version lists are stored in the Azure Blob Storage container for [search auxiliary files](Search-auxiliary-files.md), in the `version-lists` folder. There is a JSON blob for each package ID where the blob's name is the package ID, lower-cased.

Say package `Foo.Bar` has four versions:

* `1.0.0` - This version is unlisted and should be hidden from search results
* `2.0.0` - This version is listed
* `3.0.0` - This version is listed is requires SemVer 2.0.0 support
* `4.0.0-prerelease` This version is listed and is prerelease

`Foo.Bar` would have a blob named `version-lists/foo.bar.json` with content:

```json
{
  "VersionProperties": {
    "1.0.0": {},
    "2.0.0": {
      "Listed": true
    },
    "3.0.0": {
      "Listed": true,
      "SemVer2": true
    },
    "4.0.0": {
      "Listed": true
    },
  }
}
```

Notice that properties with `false` values are omitted. An unlisted version that does not require SemVer 2.0.0 does not have any properties.

The order of versions within the `VersionProperties` object is undefined.