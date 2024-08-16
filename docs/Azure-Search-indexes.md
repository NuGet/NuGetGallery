# Azure Search indexes

**Subsystem: Search 🔎**

The search subsystem heavily depends on Azure Search for storing package metadata and performing package queries. Within
a single Azure Search resource, there can be multiple indexes. An index is simply a collection of documents with a
common schema. For the NuGet search subsystem, there are two indexes expected in each Azure Search resource:

- [`search-XXX`](#search-index) - this is the "search" index which contains documents for *discovery* queries
- [`hijack-XXX`](#hijack-index) - this is the "hijack" index which contains documents for *metadata lookup* queries

## Search index

The search index is designed to fulfill queries for package discovery. This is likely the scenario you would think about
first when you imagine how package search would work. It's optimized for searching package metadata field by one or more
keywords and has a scoring profile that returns the most relevant package first.

This index has up to four documents per package ID. Each of the four ID-specific documents represents a different view
of available package versions. There are two factors for filtering in and out package versions: whether or not to
consider prerelease versions and whether or not to consider SemVer 2.0.0 versions.

This may seem is a little strange at first, so it's best to consider an example. Consider a package
[`BaseTestPackage.SearchFilters`](https://www.nuget.org/packages/BaseTestPackage.SearchFilters) that has four versions:

- `1.1.0` - stable, SemVer 1.0.0
- `1.2.0-beta`, prerelease, SemVer 1.0.0
- `1.3.0+metadata`, stable, SemVer 2.0.0 (due to build metadata)
- `1.4.0-delta.4`, prerelease, SemVer 2.0.0 (due to a dot in the prerelease label)

As mentioned before there are up to four documents per package ID. In the case of the example package
`BaseTestPackage.SearchFilters`, there will be four documents, each with a different set of versions included in the
document.

- Stable + SemVer 1.0.0: contains only `1.1.0` ([example query](https://azuresearch-usnc.nuget.org/query?q=packageid:BaseTestPackage.SearchFilters))
- Stable/Prerelease + SemVer 1.0.0: contains `1.1.0` and `1.2.0-beta` ([example query](https://azuresearch-usnc.nuget.org/query?q=packageid:BaseTestPackage.SearchFilters&prerelease=true))
- Stable + SemVer 2.0.0: contains `1.1.0` and `1.3.0+metadata` ([example query](https://azuresearch-usnc.nuget.org/query?q=packageid:BaseTestPackage.SearchFilters&semVerLevel=2.0.0))
- Stable/Prerelease + SemVer 2.0.0: contains all versions ([example query](https://azuresearch-usnc.nuget.org/query?q=packageid:BaseTestPackage.SearchFilters&prerelease=true&semVerLevel=2.0.0))

The four "flavors" of search documents per ID are referred to as **search filters**.

The documents in the search index are identified (via the `key` property) by a unique string with the following format:

```
{sanitized lowercase ID}-{base64 lowercase ID}-{search filter}
```

The `sanitized lowercase ID` removes all characters from the package ID that are not acceptable for Azure Search
document keys, like dots and non-ASCII word characters (like Chinese characters). This component of the document key is
included for readability purposes only.

The `base64 lowercase ID` is the base64 encoding of the package ID's bytes, encoded with UTF-8. This string is
guaranteed to be a 1:1 mapping with the lowercase package ID and is included for uniqueness. The
`HttpServerUtility.UrlTokenEncode` API is used for base64 encoding.

The `search filter` has one of four values:

- `Default` - Stable + SemVer 1.0.0
- `IncludePrerelease` - Stable/Prerelease + SemVer 1.0.0
- `IncludeSemVer2` - Stable + SemVer 2.0.0
- `IncludePrereleaseAndSemVer2` - Stable/Prerelease + SemVer 2.0.0

For the package ID `BaseTestPackage.SearchFilters`, the Stable + 1.0.0 document key would be:

```
basetestpackage_searchfilters-YmFzZXRlc3RwYWNrYWdlLnNlYXJjaGZpbHRlcnM1-Default
```

Each document contains a variety of metadata fields originating from the latest version in the application version list
as well as a field listing all versions. See the
[`NuGet.Services.AzureSearch.SearchDocument.Full`](../src/NuGet.Services.AzureSearch/Models/SearchDocument.cs) class and
its inherited members for a full list of the fields.

Unlisted package versions do not appear in the search index at all.

## Hijack index

The hijack index is used by the gallery to fulfill specific metadata lookup operations. For example, if a
customer is looking for metadata about all versions of the package ID `Newtonsoft.Json`, in certain cases the gallery
will query the search service for this metadata and the search service will use the hijack index to fetch the
data.

This index has one document for every version of every package ID, whether it is unlisted or not. The search service
uses this index to find all versions of a package via the `ignoreFilter=true` parameter including,

- unlisted packages ([example query](https://azuresearch-usnc.nuget.org/search/query?q=packageid:BaseTestPackage.Unlisted&ignoreFilter=true))
- multiple versions of a single ID ([example query](https://azuresearch-usnc.nuget.org/search/query?q=packageid:BaseTestPackage.SearchFilters&ignoreFilter=true&semVerLevel=2.0.0))

The documents in the hijack index are identified (via the `key` property) by a unique string with the following format:

```
{sanitized ID/version}-{base64 ID/version}
```

The `sanitized ID/version` removes all characters from the `{lowercase package ID}/{lowercase, normalized version}`
that are not acceptable for Azure Search document keys, like dots and non-ASCII word characters (like Chinese
characters). This component of the document key is included for readability purposes only.

The `base64 ID/version` is the base64 encoding of the previously mentioned concatenation of ID and version, encoded
with UTF-8. This string is guaranteed to be a 1:1 mapping with the lowercase package ID and version and is included
for uniqueness. The `HttpServerUtility.UrlTokenEncode` API is used for base64 encoding.

For the package ID `BaseTestPackage.SearchFilters` and version `1.3.0+metadata`, the document key would be:

```
basetestpackage_searchfilters_1_3_0-YmFzZXRlc3RwYWNrYWdlLnNlYXJjaGZpbHRlcnMvMS4zLjA1
```

Each document contains a variety of metadata fields originating from the latest version in the application version list
as well as a field listing all versions. See the
[`NuGet.Services.AzureSearch.HijackDocument.Full`](../src/NuGet.Services.AzureSearch/Models/HijackDocument.cs) class and
its inherited members for a full list of the fields.
