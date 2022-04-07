## Overview

**Subsystem: Search 🔎**

This job updates the [Azure Search indexes](../../docs/Azure-Search-indexes.md) used by the [search service](../NuGet.Services.SearchService).

`Catalog2AzureSearch` uses the [catalog resource](https://docs.microsoft.com/en-us/nuget/api/catalog-resource) to track package events, like uploads and deletes. It also uses the [package metadata resource](https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource) to fetch packages' metadata. Finally, it tracks the latest versions of packages using the  [version list resource](../../docs/Search-version-list-resource.md).

## Running the job

You can run this job using:

```ps1
NuGet.Jobs.Catalog2AzureSearch.exe -Configuration path\to\your\settings.json
```

This job is a singleton. Only a single instance of the job should be running per Azure Search resource.

### Using DEV resources

The easiest way to run the tool if you are on the nuget.org team is to use the DEV environment resources:

1. Install the certificate used to authenticate as our client AAD app registration into your `CurrentUser` certificate store.
1. Clone our internal [`NuGetDeployment`](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeploymentp) repository.
1. Update your cloned copy of the [DEV Catalog2AzureSearch appsettings.json](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeployment?path=%2Fsrc%2FJobs%2FNuGet.Jobs.Cloud%2FJobs%2FCatalog2AzureSearch%2FDEV%2Fnorthcentralus%2Fa%2Fappsettings.json) file to authenticate using the certificate you installed:
```json
{
    ...
    "KeyVault_VaultName": "PLACEHOLDER",
    "KeyVault_ClientId": "PLACEHOLDER",
    "KeyVault_CertificateThumbprint": "PLACEHOLDER",
    "KeyVault_ValidateCertificate": true,
    "KeyVault_StoreName": "My",
    "KeyVault_StoreLocation": "CurrentUser"
    ...
}
```

1. Update the `-Configuration` CLI option to point to the DEV Azure Search settings: `NuGetDeployment/src/Jobs/NuGet.Jobs.Cloud/Jobs/Catalog2AzureSearch/DEV/northcentralus/a/appsettings.json`

### Using personal Azure resources

As an alternative to using nuget.org's DEV resources, you can also run this tool using your personal Azure resources.

#### Prerequisites

Run the [`Db2AzureSearch`](../NuGet.Jobs.Db2AzureSearch) tool.

#### Settings

Once you've created your Azure resources, you can create your `settings.json` file. There's a few `PLACEHOLDER` values you will need to fill in yourself:

* The `GalleryDb:ConnectionString` setting is the connection string to your Gallery DB.
* The `SearchServiceName` setting is the name of your Azure Search resource. For example, use the name `foo-bar` for the Azure Search service with URL `https://foo-bar.search.windows.net`.
* The `SearchServiceApiKey` setting is an admin key that has write permissions to the Azure Search resource.
* The `StorageConnectionString` setting is the connection string to your Azure Blob Storage account.

```json
{
  "GalleryDb": {
    "ConnectionString": "PLACEHOLDER"
  },

  "Catalog2AzureSearch": {
    "AzureSearchBatchSize": 1000,
    "MaxConcurrentBatches": 4,
    "MaxConcurrentVersionListWriters": 8,
    "SearchServiceName": "PLACEHOLDER",
    "SearchServiceApiKey": "PLACEHOLDER",
    "SearchIndexName": "search-000",
    "HijackIndexName": "hijack-000",
    "StorageConnectionString": "PLACEHOLDER",
    "StorageContainer": "v3-azuresearch-000",
    "StoragePath": "",
    "GalleryBaseUrl": "https://www.nuget.org/",
    "FlatContainerBaseUrl": "https://api.nuget.org/",
    "FlatContainerContainerName": "v3-flatcontainer",
    "AllIconsInFlatContainer": false,
    "Source": "https://api.nuget.org/v3/catalog0/index.json",
    "HttpClientTimeout": "00:10:00",
    "DependencyCursorUrls": [
      "https://nugetgallery.blob.core.windows.net/v3-registration5-semver1/cursor.json"
    ],
    "RegistrationsBaseUrl": "https://api.nuget.org/v3/registration5-gz-semver2/"
  }
}
```

## Algorithm

At a high-level, here's how Catalog2AzureSearch works:

1. Load its catalog cursor from Azure Blob Storage
1. Fetch catalog leaves that are newer than the catalog cursor value
1. For each package ID in the catalog leaves:
    1. Fetch the [version list resource](../../docs/Search-version-list-resource.md) for the package ID
    1. Apply the package's catalog leaves to the version list resource to understand which search documents need to be updated. In some cases, use the [Package Metadata resource](https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource) to fetch additional package metadata and catalog leaves
    1. Generate Azure Search  actions to update the indexes
1. Push all generated Azure Search index actions
1. Save the catalog cursor to Azure Blob Storage
