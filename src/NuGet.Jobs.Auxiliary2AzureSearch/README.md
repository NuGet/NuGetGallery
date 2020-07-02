## Overview

**Subsystem: Search 🔎**

This job updates the [search auxiliary files](../../docs/Search-auxiliary-files.md) used by the [search service](../NuGet.Services.SearchService). This also updates the downloads and owners data in the [Azure Search "search" index](../../docs/Azure-Search-indexes.md).

## Running the job

You can run this job using:

```ps1
NuGet.Jobs.Auxiliary2AzureSearch.exe -Configuration path\to\your\settings.json
```

This job is a singleton. Only a single instance of the job should be running per Azure Search resource.

### Using DEV resources

The easiest way to run the tool if you are on the nuget.org team is to use the DEV environment resources:

1. Install the certificate used to authenticate as our client AAD app registration into your `CurrentUser` certificate store.
1. Clone our internal [`NuGetDeployment`](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeploymentp) repository.
1. Update your cloned copy of the [DEV Auxiliary2AzureSearch appsettings.json](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeployment?path=%2Fsrc%2FJobs%2FNuGet.Jobs.Cloud%2FJobs%2FAuxiliary2AzureSearch%2FDEV%2Fnorthcentralus%2Fa%2Fappsettings.json) file to authenticate using the certificate you installed:
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

1. Update the `-Configuration` CLI option to point to the DEV Azure Search settings: `NuGetDeployment/src/Jobs/NuGet.Jobs.Cloud/Jobs/Auxiliary2AzureSearch/DEV/northcentralus/a/appsettings.json`

### Using personal Azure resources

As an alternative to using nuget.org's DEV resources, you can also run this tool using your personal Azure resources.

#### Prerequisites

Run the [`Db2AzureSearch`](../NuGet.Jobs.Db2AzureSearch) tool.

#### Settings

Once you've created your Azure resources, you can create your `settings.json` file. There's a few `PLACEHOLDER` values you will need to fill in yourself:

* The `GalleryDb:ConnectionString` setting is the connection string to your Gallery DB.
* The `SearchServiceName` setting is the name of your Azure Search resource. For example, use the name `foo-bar` for the Azure Search service with URL `https://foo-bar.search.windows.net`.
* The `SearchServiceApiKey` setting is an admin key that has write permissions to the Azure Search resource.
* The `AuxiliaryDataStorageContainer` and `StorageConnectionString` settings are the connection strings to your Azure Blob Storage account.

```json
{
  "GalleryDb": {
    "ConnectionString": "PLACEHOLDER"
  },

  "Auxiliary2AzureSearch": {
    "AzureSearchBatchSize": 1000,
    "MaxConcurrentBatches": 1,
    "MaxConcurrentVersionListWriters": 32,
    "SearchServiceName": "PLACEHOLDER",
    "SearchServiceApiKey": "PLACEHOLDER",
    "SearchIndexName": "search-000",
    "HijackIndexName": "hijack-000",
    "StorageConnectionString": "PLACEHOLDER",
    "StorageContainer": "v3-azuresearch-000",
    "StoragePath": "",
    "AuxiliaryDataStorageConnectionString": "PLACEHOLDER",
    "AuxiliaryDataStorageContainer": "ng-search-data",
    "AuxiliaryDataStorageDownloadsPath": "downloads.v1.json",
    "AuxiliaryDataStorageDownloadOverridesPath": "downloadOverrides.json",
    "AuxiliaryDataStorageExcludedPackagesPath": "ExcludedPackages.v1.json",
    "AuxiliaryDataStorageVerifiedPackagesPath": "verifiedPackages.json",
    "MinPushPeriod": "00:00:10",
    "MaxDownloadCountDecreases": 30000,
    "EnablePopularityTransfers": true,
    "Scoring": {
      "PopularityTransfer": 0.99
    }
  }
}
```

## Algorithm

At a high-level, here's how Auxiliary2AzureSearch works:

1. Update verified packages
    1. Get the "old" list of verified package IDs from search auxiliary storage
    2. Get the "new" list of verified package IDs from Gallery DB
    3. Replace the verified package auxiliary file if needed
1. Update downloads
    1. Get the "old" downloads data from search auxiliary storage
    1. Get the "new" downloads data from statistics auxiliary storage
    1. Determine which packages have download changes
    1. Get the "old" popularity transfers data from search auxiliary storage
    1. Get the "new" popularity transfers data from statistics auxiliary storage
    1. Determine which packages have popularity transfer changes
    1. Update Azure Search documents in the "search" index to reflect the latest downloads and popularity transfers
    1. Save the "new" downloads data to the search auxiliary storage
    1. Save the "new" popularity transfers data to search auxiliary storage
1. Update owners
    1. Get the "old" owners data from search auxiliary storage
    1. Get the "new" owners data from Gallery DB
    1. Update Azure Search documents in the "search" index to reflect the ownership changes, if any
    1. Track ownership changes in search auxiliary storage
    1. Save the "new" owners data to the search auxiliary storage