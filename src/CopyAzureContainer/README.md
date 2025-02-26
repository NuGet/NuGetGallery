# Overview

TLDR: It copies containers from a source storage to a destination storage to create backup files for later restore if needed. Also, it uses both Azure Storage Blob SDK and AzCopy tool.

This job is a tool that copies all the content from multiple source containers from a source storage to a destination storage that creates a backup container for each of the source containers. Each backup container on the destination storage is deleted if they have been created before an specified **BackupDays**. These backup containers can be used to restore all files on their source.

This job uses Azure Blob Storage SDK to delete and create containers on the destination storage and the [AzCopy v10](https://learn.microsoft.com/azure/storage/common/storage-use-azcopy-v10?tabs=dnf) tool to perform the copy operation between storages.

When the job runs, it creates a new folder locally where AzCopy files like logs will be stored, these files will also be uploaded to the destination storage in the logs container for investigation in case something wrong happened in the AzCopy operation. This folder is cleaned on every new run.

This job uses 3 types of login defined on the appsettings file:
* If Sas tokens are provided:
    * For Storage SDK: It uses the `AzureSasCredential`.
    * For AzCopy: The job adds the sas tokens for the storages on the copy commands.
* If Managed Identity is provided:
    * For Storage SDK: It creates clients with `DefaultAzureCredential` or `ManagedIdentityCredential` if a managed identity client id is provided.
    * For AzCopy:
        * NOTE: Only used for system or user assigned MSI on VMs not for testing locally.
        * It uses AzCopy `AZCOPY_AUTO_LOGIN_TYPE=MSI` environment variable.
        * If a managed identity client id is provided it stores it in `AZCOPY_MSI_CLIENT_ID=<msi-client-id>` environment variable.
* When Debugging locally for AzCopy if no SAS tokens are provided:
    * The CopyAzureContainer job will run the `azcopy login` command that will generate a code on the terminal, you should just follow the instructions to login.

# Algorithm

1. It creates a logs container on the destination storage.
1. For each specified source container does the following:
    1. Deletes all destination containers that have been created before the specified **BackupDays** that has the source container prefix.
        1. e.g. If a source container is `catalog` and the destination backup container is `catalog-<date>`. it will delete all of the containers that starts with `catalog` if they are older than the BackupDays.
    1. It performs the copy operation for that source container, the steps are the following:
        1. It creates a local folder to store AzCopy files.
            1. `AZCOPY_JOB_PLAN_LOCATION` environment variable tells AzCopy where to store job plan files.
            1. `AZCOPY_LOG_LOCATION` environment variable tells AzCopy where to store log files.
        1. It creates a new container on the destination storage to contain all the files from the source container.
            1. This new container name is created with the format `<source container name>-<date>`. e.g. `catalog` container on source storage is `catalog-2025010800` on the destination storage. This to keep multiple backups of the same source container.
        1. It generates the commands that AzCopy will use to copy the container contents, and the logs.
        1. If you are on Debug mode and didn't provide SAS tokens for storage access it will run the `azcopy login` command, this will provide instructions on the terminal on how to authenticate.
        1. Then it will run the AzCopy tool to run the copy command to copy the source container content to the destination container previously created.
        1. After it copied all the content, it will upload the logs on the destination storage logs container.


# Running the job

## Prerequisites

1. Azure Storage account.
    * You can reuse an storage account or create a new one.
    * Make sure to have at least a container with files on your source storage to test the behavior.
    * If you are using managed identities for Storage SDK usage make sure to have `Storage Blob Data Contributor` access on your identity.
1. Create the appsettings.json file.
    * This will contained the information needed for the job to run.
    * Example of a configuration file using SAS tokens.
    ```
    {
      "CopyAzureContainer": {
        "BackupDays": 14, // Amount of days the destination storage should keep backup containers before deletion.
        "DestStorageAccountName": "destinationstorage", // Destination storage account name
        "DestStorageSasValue": "<destination storage sas token>", // Destination storage sas token
        "SourceContainers": [
          {
            "StorageAccountName": "sourcestorage1", // Source storage account name
            "StorageSasToken": "<source storage sas token>", // Source storage sas token
            "ContainerName": "sourcecontainer" // Source container that will be copied to the destination
          }
        ]
      },
      "KeyVault_VaultName": "PLACEHOLDER",
      "KeyVault_UseManagedIdentity": true
    }
    ```

    * Example of a configuration file using MSI. (just remove the properties for sas values and add the `Storage_UseManagedIdentity` property)
    ```
    {
      "CopyAzureContainer": {
        "BackupDays": 14,
        "DestStorageAccountName": "destinationstorage",
        "SourceContainers": [
          {
            "StorageAccountName": "sourcestorage1",
            "ContainerName": "sourcecontainer"
          }
        ]
      },
      "KeyVault_VaultName": "PLACEHOLDER",
      "KeyVault_UseManagedIdentity": true,
      "Storage_UseManagedIdentity": true
    }
    ```
1. Download AzCopy tool
    1. Execute the `Scripts/InstallAzCopy.ps1` script to download AzCopy tool.
    1. Make sure the `azcopy.exe` file was added on your `bin\Debug\tools\azcopy` or directory where your binaries are like `CopyAzureContainer\bin\Debug\net472\tools\azcopy`.

## Execute the job

1. Build the project and go to where the binaries are.
2. Place the appsettings.json file in the same folder as the binaries.
3. Make sure you have the AzCopy tool downloaded on `<path to your binaries>/tool/azcopy/azcopy.exe`
4. Wait until the job is completed.


# Resources

* [Get started with AzCopy](https://learn.microsoft.com/azure/storage/common/storage-use-azcopy-v10?tabs=dnf)
* [Authorize access to blobs and files with AzCopy and Microsoft Entra ID](https://learn.microsoft.com/azure/storage/common/storage-use-azcopy-authorize-azure-active-directory)
* [AzCopy v10 configuration settings (Azure Storage)](https://learn.microsoft.com/azure/storage/common/storage-ref-azcopy-configuration-settings)
* [Troubleshoot issues in AzCopy v10](https://learn.microsoft.com/troubleshoot/azure/azure-storage/blobs/connectivity/storage-use-azcopy-troubleshoot)
