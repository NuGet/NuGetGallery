## Overview

**Subsystem: Search 🔎**

This project contains the search service, the microservice for searching NuGet packages. The search service is an
ASP.NET MVC web application that communicates directly with an existing
[Azure Search](https://azure.microsoft.com/en-us/services/search/) resource in Azure. It can be considered as an adapter
between clients expecting a NuGet-owned protocol and Azure Search, which returns documents with their own Azure Search
schema unrelated to NuGet.

The primary purpose of the service is to provide metadata about packages most relevant to given customer search text.
However the service has several endpoints meant for a variety of scenarios: both documented REST API contracts as well
as implementation details for [NuGetGallery](https://github.com/NuGet/NuGetGallery) (a.k.a the gallery) search
functionality.

The officially documented endpoints on the search service are:

- [`/query`](#query---v3-search-endpoint) - an implementation of the NuGet V3 API [Search resource](https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource)
- [`/autocomplete`](#autocomplete---v3-autocomplete-endpoint) - an implementation of the  NuGet V3 API [Autocomplete resource](https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource)

Several other endpoints exist as implementation details (i.e. their API surface area is not guaranteed to be stable):

- [`/search/query`](#searchquery---internal-v2-search-endpoint) - used by the NuGetGallery to fulfill package searches as well some V2 OData queries
- [`/search/diag`](#searchdiag---diagnostic-information-for-monitoring) - used by monitoring for diagnostic information about a running instance of the service
- [`/`](#---health-endpoint-for-load-balancers) - used by infrastructure (such as a load balancer) in front of the service to determine if it is healthy, i.e. a health probe

The search service can be considered a read-only, egress service. The service expects the configured Azure Search
resource to already be populated with package metadata. The responsibility of the service is to accept user queries,
map the queries to Azure Search REST API calls, and map the resulting Azure Search documents to a JSON shape that the
customer expects.

The search text is the  value passed to the `q` parameter on the `/query` and `/search/query` endpoints. This search
text supports a [basic set of operations](https://docs.microsoft.com/en-us/nuget/consume-packages/finding-and-choosing-packages#search-syntax),
loosely mimicking a small subset of Lucene syntax.

No authentication is required for accessing any of the endpoints on the service. All endpoints support HTTP GET, receive
any parameters via the query string, and return JSON.

## Multiple service instances ✅

This service is read-only. Therefore, as many instances of the service can be deployed as desired and there will be no
concurrency issues. In fact, nuget.org deploys at least 2 instances of the service to 4 distinct Azure regions. Each
region has its own search service and Azure Search resource (for BCDR reasons).

The service itself is stateless, depending on external state that is persisted in the configured Azure Search resource.

## Azure Search indexes

For more information about the Azure Search indexes that the search service uses, see
[Azure Search indexes](../../docs/Azure-Search-indexes.md). Both the search and hijack index are used.

## Auxiliary files

For more information about all files used by the search subsystem, see [search auxiliary files](../../docs/Search-auxiliary-files.md).
A subset of the auxiliary files are used by the search service. The files used are:

  - [`downloads/downloads.v2.json`](../../docs/Search-auxiliary-files.md#download-count-data) - for stitching the latest download count number, per ID and version
  - [`verified-packages/verified-packages.v1.json`](../../docs/Search-auxiliary-files.md#verified-packages-data) - for the verified boolean
  - [`popularity-transfers/popularity-transfers.v1.json`](../../docs/Search-auxiliary-files.md#popularity-transfer-data) - for monitoring

## Endpoints

All endpoints provided the service exist on the [`SearchController`](Controllers/SearchController.cs).

### `/query` - V3 search endpoint

This endpoint is used primarily by Visual Studio Package Manager UI. When user's click "Manage NuGet Packages" in
Visual Studio and select the "Browse" tab, search will go directly against the search service. As of .NET 5.0, the
.NET CLI queries the search service via the `dotnet tool search` command to search for packages with package type
`DotnetTool`. On nuget.org, this endpoint is also heavily used by third party applications and scripts.

As with all V3 resources, the specific URL that the client uses is discovered from the
[service index](https://docs.microsoft.com/en-us/nuget/api/service-index). Additionally, this endpoint supports an
optional `debug=true` parameter which shows the raw Azure Search document and other diagnostic information.

The parameters and response body are documented in the NuGet V3 API
[Search resource](https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource) reference.

This endpoint exclusively uses the [search index](../../docs/Azure-Search-indexes.md#search-index).

### `/autocomplete` - V3 autocomplete endpoint

This endpoint is used by the Package Manager Console in Visual Studio for package ID table completion for commands like
`Install-Package`. Historically, this endpoint was used by the project.json editor in Visual Studio, however this
scenario is now entirely deprecated.

As with all V3 resources, the specific URL that the client uses is discovered from the
[service index](https://docs.microsoft.com/en-us/nuget/api/service-index).

The parameters and response body are documented in the NuGet V3 API
[Autocomplete resource](https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource) reference.
Additionally, this endpoint supports an optional `debug=true` parameter which shows the raw Azure Search document and
other diagnostic information.

This endpoint exclusively uses the [search index](../../docs/Azure-Search-indexes.md#search-index) to enumerate both
package IDs and package versions.

### `/search/query` - internal V2 search endpoint

This endpoint is used by NuGetGallery in several scenarios when the gallery is configured with an external search
service. Therefore, the contract should be considered unstable by external clients and may change freely to match the
requirements of NuGetGallery. We strongly urge external applications (even NuGet client) to avoid using this endpoint
since it should be considered and implementation detail of NuGetGallery. External clients should use the document V3
Search endpoint and discover the URL via the service index.

When the NuGetGallery code "hijacks" an OData query to search service instead of going to the SQL database, this
endpoint is used. Here is a non-exhaustive list of how NuGetGallery endpoints call into search service:

- V2 API, search
  - Gallery URL: `/api/v2/Search()?q=%27json%27`
  - Search URL: `/search/query?q=json&skip=0&take=100&sortBy=relevance&luceneQuery=false`
  - This is used for package search scenarios much like [V3 search](#query---v3-search-endpoint) when the client is using a V2
    source URL pointing to NuGetGallery.
- V2 API, get metadata of all versions of an ID
  - Gallery URL: `/api/v2/FindPackagesById()?id=%27Newtonsoft.Json%27`
  - Search URL: `/search/query?q=Id%3A%22Newtonsoft.Json%22&skip=0&take=100&sortBy=created-asc&prerelease=true&ignoreFilter=true`
  - This is used for package restore scenarios much like the V3 endpoint to [enumerate package versions](https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource#enumerate-package-versions)
    but when the client is using ta V2 source URL pointing to NuGetGallery.
- V2 API, get metadata about a specific version
  - Gallery URL: `/api/v2/Packages(Id='Newtonsoft.Json',Version='9.0.1')`
  - Search URL: `/search/query?q=Id%3A%22Newtonsoft.Json%22+AND+Version%3A%229.0.1%22&skip=0&take=1&sortBy=created-asc&semVerLevel=2.0.0&prerelease=true&ignoreFilter=true`
- Gallery UI, search
  - Gallery URL: `/packages?q=json`
  - Search URL: `/search/query?q=json&skip=0&take=20&sortBy=relevance&semVerLevel=2.0.0&prerelease=true&luceneQuery=false`

This endpoint uses both the [search index](../../docs/Azure-Search-indexes.md#search-index) and the [hijack index](../../docs/Azure-Search-indexes.md#hijack-index),
depending on the `ignoreFilter` parameter.

#### Request parameters

Name         | Type    | Notes
------------ | ------- | -----
q            | string  | The search terms used to filter packages
skip         | integer | The number of results to skip, for pagination
take         | integer | The number of results to return, for pagination
prerelease   | boolean | `true` or `false` determining whether to include prerelease packages
semVerLevel  | string  | A SemVer 1.0.0 version string: `1.0.0` or `2.0.0`
ignoreFilter | boolean | `true` to include unlisted packages and ignore the `prerelease` parameter
countOnly    | boolean | `true` to return only the total count and no metadata
sortBy       | string  | Sort results using a specified ordering
luceneQuery  | bool    | `true` to treat a `q` starting with `id:` like `packageid:` (yes, it's silly, see [#7366](https://github.com/NuGet/NuGetGallery/issues/7366))
debug        | bool    | `true` to shows the raw Azure Search document and other diagnostic information

If no `q` is provided, all packages should be returned, within the boundaries imposed by skip and take.

The `skip` parameter defaults to 0. The maximum value is 10000.

The `take` parameter should be an integer greater than zero. The default value is 20. The maximum value is 1000.

If `prerelease` is not provided, prerelease packages are excluded.

The `semVerLevel` query parameter is used to opt-in to
[SemVer 2.0.0 packages](https://github.com/NuGet/Home/wiki/SemVer2-support-for-nuget.org-%28server-side%29#identifying-semver-v200-packages).
If this query parameter is excluded, only packages with SemVer 1.0.0 compatible versions will be returned (with the 
[standard NuGet versioning](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning) caveats, such as version strings with 4 integer pieces).
If `semVerLevel=2.0.0` is provided, both SemVer 1.0.0 and SemVer 2.0.0 compatible packages will be returned. See the
[SemVer 2.0.0 support for nuget.org](https://github.com/NuGet/Home/wiki/SemVer2-support-for-nuget.org-%28server-side%29)
for more information.

The `ignoreFilter` parameter is used to toggle between the search index and the hijack index. Standard package search
or discovery scenarios by keyword will use `ignoreFilter=false`. Metadata look-up scenarios (mainly for NuGet restore)
will use `ignoreFilter=true` which allows the metadata of non-latest and unlisted packages to be seen. The `semVerLevel`
parameter still applies when `ignoreFilter=true` (i.e. not all filters are ignored).

The `sortBy` parameter supports the following options:

- `relevance` - sort by relevance, most relevant at the top
- `lastEdited` - sort by last edited timestamp, descending chronological order
- `published` - sort by published timestamp, descending chronological order
- `title-asc` - sort by title, ascending case-insensitive lexicographical order
- `title-desc` - sort by title, descending case-insensitive lexicographical order
- `created-asc` - sort by created timestamp, ascending chronological order
- `created-desc` - sort by created timestamp, descending chronological order

The `relevance` value is used by default or if the provided value is not supported.

For `title-asc` and `title-desc`, a package's ID is used if the package has no explicit title value. Given that package
`title` is no longer prominently shown in NuGet experiences, this sorting order is only maintained for legacy reasons.

#### Response

The response is different than the V3 Search response but shares many of the same fields. The fields have slightly 
different names (PascalCase instead of camelCase) and are arranged differently. Some of the main differences are:

- `/search/query` has the following fields that `/query` does not have:
  - `Owners` - not present when querying the hijack index
  - `Version` - the verbatim/original version found in the .nuspec
  - `Copyright`
  - `Language`
  - `ReleaseNotes`
  - `IsLatest` and `IsLatestStable` - respects the the `semVerLevel` parameter, only interesting when `ignoreFilter=true`
  - `Listed` - only interesting when `ignoreFilter=true`
  - `Created` - created timestamp
  - `Published` - published timestamp
  - `LastEdited` - last edited timestamp
  - `FlattenedDependencies` - the package dependencies structured data but as a flat string using a custom encoding
  - `MinClientVersion`
  - `Hash` - base64 encoded
  - `HashAlgorithm`
  - `PackageFileSize`
  - `RequiresLicenseAcceptance`
- `/query` has the following fields that `/search/query` does not have:
  - `packageTypes` - array of package type objects
  - `versions` - full list of versions for that package ID

The `LastUpdated` property has the same value as `Published` for legacy reasons.

The `Dependencies` and `SupportedFrameworks` fields are always empty arrays because NuGetGallery does not used these
values but expects the properties to be present.

#### Sample response

```json
{
  "totalHits": 1,
  "data": [
    {
      "PackageRegistration": {
        "Id": "BaseTestPackage.Unlisted",
        "DownloadCount": 93,
        "Verified": false,
        "Owners": [],
        "PopularityTransfers": []
      },
      "Version": "1.1.0",
      "NormalizedVersion": "1.1.0",
      "Title": "BaseTestPackage.Unlisted",
      "Description": "A package for testing unlisted status.",
      "Summary": "",
      "Authors": "jver",
      "Tags": "",
      "IsLatestStable": false,
      "IsLatest": false,
      "Listed": false,
      "Created": "2019-07-22T15:48:32.107+00:00",
      "Published": "1900-01-01T00:00:00+00:00",
      "LastUpdated": "1900-01-01T00:00:00+00:00",
      "LastEdited": "2019-07-22T15:52:43.053+00:00",
      "DownloadCount": 93,
      "FlattenedDependencies": "",
      "Dependencies": [],
      "SupportedFrameworks": [],
      "Hash": "kRvVPTmvFRa+EaKmYJhitnHbZLexclm3fLtKJGwigbExRlrmOCtYH+zXfGSeuxCE980x3aSgqwM9V5PaNlnFRw==",
      "HashAlgorithm": "SHA512",
      "PackageFileSize": 9988,
      "RequiresLicenseAcceptance": false
    }
  ]
}
```

### `/search/diag` - diagnostic information for monitoring

This endpoint is used to show diagnostic information. This enables the following monitoring scenarios:

- recent secret reload from KeyVault, via the `Server.LastServiceRefreshTime` property
- latest catalog data is in the search index, via the `SearchIndex.LastCommitTimestamp` property
- latest catalog data is in the hijack index, via the `HijackIndex.LastCommitTimestamp` property
- recent auxiliary file reload, via the `AuxiliaryFiles.Loaded` property

The other properties are just helpful for live site investigations.

The contract of the response body is unstable and can change freely over time given the internal monitoring systems
react to the changes appropriately. External client software should not use this endpoint.

An HTTP `200 OK` is returned if the minimum dependencies are available for the search service. If there is a problem,
HTTP `500 Internal Server Error` is returned.


### `/` - health endpoint for load balancers

The endpoint internally fetches the same data as `/search/diag` but only returns a simple success boolean.

An HTTP `200 OK` is returned if the minimum dependencies are available for the search service. If there is a problem,
HTTP `500 Internal Server Error` is returned.

## Running the service

Uses one of the following approaches to modify the `Settings/local.json` file with configuration
values. Once this configuration files has the settings you'd like, you can launch the service in Visual Studio using
<kbd>F5</kbd>. This will start the service in IIS Express and open your web browser to the running service.

### Using personal Azure resources

To use you own resources, you need to initialize the indexes in an Azure Search resource and the auxiliary files in
Azure Blob Storage. The easiest way to do this is using the [Db2AzureSearch tool](../NuGet.Jobs.Db2AzureSearch/README.md).
This tool populates the search and hijack indexes in the configured Azure Search resource and initialize the initial
auxiliary data files. After the tool finishes, simply configure the Search Service to point to the same Azure Search and
Blob Storage container.

You don't necessarily need to run Catalog2AzureSearch or Auxiliary2AzureSearch since these two jobs keep the indexes and
auxiliary files up to data after Db2AzureSearch has already initialized the data. For testing, you can typically get by
with static data that isn't staying up to data all the time.

The `PLACEHOLDER` values need to match whatever you used when running Db2AzureSearch, except for `SearchServiceApiKey`
which can be a read-only (query) key instead of the admin key used by Db2AzureSearch.

The `ApplicationInsights_InstrumentationKey` setting is optional and can be removed.

You can use different values for `FlatContainerBaseUrl`, `FlatContainerContainerName`, `SemVer1RegistrationsBaseUrl`,
and `SemVer2RegistrationsBaseUrl` but the impact is low. These settings are just used for building URLs returned in the
service responses and are not called into at runtime.

```json
{
  "ApplicationInsights_InstrumentationKey": "PLACEHOLDER",
  "SearchService": {
    "AllIconsInFlatContainer": true,
    "FlatContainerBaseUrl": "https://apidev.nugettest.org/",
    "FlatContainerContainerName": "v3-flatcontainer",
    "HijackIndexName": "PLACEHOLDER",
    "SearchIndexName": "PLACEHOLDER",
    "SearchServiceApiKey": "PLACEHOLDER",
    "SearchServiceName": "PLACEHOLDER",
    "SemVer1RegistrationsBaseUrl": "https://apidev.nugettest.org/v3/registration5-semver1/",
    "SemVer2RegistrationsBaseUrl": "https://apidev.nugettest.org/v3/registration5-gz-semver2/",
    "StorageConnectionString": "PLACEHOLDER",
    "StorageContainer": "PLACEHOLDER"
  }
}
```

### Using DEV resources

The easiest way to run the job if you are on the nuget.org server team is to use the DEV environment resources. This can
be done by pointing configuration to a DEV Azure Search region and installing the certificate used to authenticate as
our client AAD app registration into your `CurrentUser` certificate store. You can look up the `PLACEHOLDER` values in
the internal deployment/configuration repository. The `XXX` is the current index number used in DEV.

The `ApplicationInsights_InstrumentationKey` setting is optional and can be removed.

```json
{
  "ApplicationInsights_InstrumentationKey": "PLACEHOLDER",
  "KeyVault_ClientId": "PLACEHOLDER",
  "KeyVault_CertificateThumbprint": "PLACEHOLDER",
  "KeyVault_ValidateCertificate": true,
  "KeyVault_StoreName": "My",
  "KeyVault_StoreLocation": "CurrentUser",
  "KeyVault_VaultName": "PLACEHOLDER",
  "SearchService": {
    "AllIconsInFlatContainer": true,
    "FlatContainerBaseUrl": "https://apidev.nugettest.org/",
    "FlatContainerContainerName": "v3-flatcontainer",
    "HijackIndexName": "hijack-XXX",
    "SearchIndexName": "search-XXX",
    "SearchServiceApiKey": "PLACEHOLDER",
    "SearchServiceName": "PLACEHOLDER",
    "SemVer1RegistrationsBaseUrl": "https://apidev.nugettest.org/v3/registration5-semver1/",
    "SemVer2RegistrationsBaseUrl": "https://apidev.nugettest.org/v3/registration5-gz-semver2/",
    "StorageConnectionString": "PLACEHOLDER",
    "StorageContainer": "v3-azuresearch-XXX"
  }
}
```

## Prerequisites

- **Azure Search**. The search and hijack indexes must already be initialized in the Azure Search resource.
- **Azure Blob Storage**. Several auxiliary files are read from Blob Storage and reloaded periodically.

For more information, please refer to the [`Db2AzureSearch`](../NuGet.Jobs.Db2AzureSearch/README.md) tool.
