## Overview

**Subsystem: Search 🔎**

This tool creates the resources needed to run the [NuGet search service](../NuGet.Services.SearchService). These resources can be updated using the [Catalog2AzureSearch](../NuGet.Jobs.Catalog2AzureSearch) and [Auxiliary2AzureSearch](../NuGet.Jobs.Auxiliary2AzureSearch) jobs.

Specifically, this tool creates:

* The [Azure Search indexes](../../docs/Azure-Search-indexes.md)
* The [search auxiliary files](../../docs/Search-auxiliary-files.md)
* The [search version list resource](../../docs/Search-version-list-resource.md)

## Running the job

You can run this job using:

```ps1
NuGet.Jobs.Db2AzureSearch.exe -Configuration path\to\your\settings.json
```

### Using DEV resources

The easiest way to run the tool if you are on the nuget.org team is to use the DEV environment resources:

1. Install the certificate used to authenticate as our client AAD app registration into your `CurrentUser` certificate store.
1. Clone our internal [`NuGetDeployment`](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeploymentp) repository.
1. Update your cloned copy of the [DEV Db2AzureSearch appsettings.json](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeployment?path=%2Fsrc%2FJobs%2FNuGet.Jobs.Cloud%2FJobs%2FDb2AzureSearch%2FDEV%2Fnorthcentralus%2Fappsettings.json) file to authenticate using the certificate you installed:
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

1. Update the `-Configuration` CLI option to point to the DEV Azure Search settings: `NuGetDeployment/src/Jobs/NuGet.Jobs.Cloud/Jobs/Db2AzureSearch/DEV/northcentralus/appsettings.json`

### Using personal Azure resources

As an alternative to using nuget.org's DEV resources, you can also run this tool using your personal Azure resources.

#### Prerequisites

- **Gallery DB**. This can be initialized locally using the [NuGetGallery](https://github.com/NuGet/NuGetGallery/blob/master/README.md).
- **Azure Search**. You can create your own Azure Search resource using the [Azure Portal](https://docs.microsoft.com/en-us/azure/search/search-create-service-portal).
- **Azure Blob Storage**. You can create your own Azure Blob Storage using the [Azure Portal](https://docs.microsoft.com/en-us/azure/storage/common/storage-account-create).

In your Azure Blob Storage account, you will need to create a container named `ng-search-data` and upload the following files:
1. `downloads.v1.json` with content `[]`
1. `ExcludedPackages.v1.json` with content `[]`

If you are on the nuget.org team, you can copy these files from the [PROD auxiliary files container](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeployment?path=%2Fsrc%2FJobs%2FNuGet.Jobs.Cloud%2FJobs%2FDb2AzureSearch%2FPROD%2Fnorthcentralus%2Fappsettings.json&version=GBmaster&line=18&lineEnd=24&lineStartColumn=1&lineEndColumn=1&lineStyle=plain).

#### Settings

Once you've created your Azure resources, you can create your `settings.json` file. There's a few `PLACEHOLDER` values you will need to fill in yourself:

* The `GalleryDb:ConnectionString` setting is the connection string to your Gallery DB.
* The `SearchServiceName` setting is the name of your Azure Search resource. For example, use the name `foo-bar` for the Azure Search service with URL `https://foo-bar.search.windows.net`.
* The `SearchServiceApiKey` setting is an admin key that has write permissions to the Azure Search resource. Make sure the Azure Search resource you're connecting to has API keys enabled (either in parallel with managed identities "RBAC" access or with managed identities authentication disabled).
* The `StorageConnectionString` and `AuxiliaryDataStorageConnectionString` settings are both the connection string to your Azure Blob Storage account.
* The `DownloadsV1JsonUrl` setting is the URL to `downloads.v1.json` file above. Make sure it works without authentication.

```json
{
  "GalleryDb": {
    "ConnectionString": "PLACEHOLDER"
  },

  "Db2AzureSearch": {
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
    "AuxiliaryDataStorageConnectionString": "PLACEHOLDER",
    "AuxiliaryDataStorageContainer": "ng-search-data",
    "AuxiliaryDataStorageExcludedPackagesPath": "ExcludedPackages.v1.json",
    "DownloadsV1JsonUrl": "PLACEHOLDER",
    "FlatContainerBaseUrl": "https://api.nuget.org/",
    "FlatContainerContainerName": "v3-flatcontainer",
    "AllIconsInFlatContainer": false,
    "DatabaseBatchSize": 10000,
    "CatalogIndexUrl": "https://api.nuget.org/v3/catalog0/index.json",
    "EnablePopularityTransfers": true,
    "Scoring": {
      "FieldWeights": {
        "PackageId": 9,
        "TokenizedPackageId": 9,
        "Tags": 5
      },
      "DownloadScoreBoost": 30000,
      "PopularityTransfer": 0.99
    }
  }
}
```

## Algorithm

At a high-level, here's how Db2AzureSearch works:

1. Create the [Azure Search indexes](../../docs/Azure-Search-indexes.md)
1. Create the Azure Blob storage container for the [search auxiliary files](../../docs/Search-auxiliary-files.md)
1. Capture the catalog's cursor
1. Load initial data from Gallery DB and statistics auxiliary files
1. Process package metadata in batches
    1. Load a chunk of packages from Gallery DB
    1. Generate and upload documents to the Azure Search indexes
    1. Update the [search version list resource](../../docs/Search-version-list-resource.md)
1. Write the [search auxiliary files](../../docs/Search-auxiliary-files.md) to search storage
1. Write the catalog's cursor to search storage