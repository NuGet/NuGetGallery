@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.China.catalog2registrationv3reg3.Title}"

title #{Jobs.China.catalog2registrationv3reg3.Title}

start /w ng.exe catalog2registration ^
    -instanceName catalog2registration-china ^
    -source #{Jobs.common.China.v3.Source} ^
    -contentBaseAddress #{Jobs.China.catalog2registrationv3reg3.ContentBaseAddress} ^
    -storageType azure ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -storageBaseAddress #{Jobs.China.catalog2registrationv3reg3.StorageBaseAddress} ^
    -storageAccountName #{Jobs.common.China.v3.StorageAccountName} ^
    -storageKeyValue #{Jobs.common.China.v3.StorageKey} ^
    -storageContainer #{Jobs.catalog2registrationv3reg3.StorageContainer} ^
    -useCompressedStorage #{Jobs.catalog2registrationv3reg3.UseCompressedStorage} ^
    -compressedStorageBaseAddress #{Jobs.China.catalog2registrationv3reg3.StorageBaseAddressCompressed} ^
    -compressedStorageAccountName #{Jobs.common.China.v3.StorageAccountName} ^
    -compressedStorageKeyValue #{Jobs.common.China.v3.StorageKey} ^
    -compressedStorageContainer #{Jobs.catalog2registrationv3reg3.StorageContainerCompressed} ^
    -useSemVer2Storage #{Jobs.catalog2registrationv3reg3.UseSemVer2Storage} ^
    -semVer2StorageBaseAddress #{Jobs.China.catalog2registrationv3reg3.StorageBaseAddressSemVer2} ^
    -semVer2StorageAccountName #{Jobs.common.China.v3.StorageAccountName} ^
    -semVer2StorageKeyValue #{Jobs.common.China.v3.StorageKey} ^
    -semVer2StorageContainer #{Jobs.catalog2registrationv3reg3.StorageContainerSemVer2} ^
    -contentIsFlatContainer #{Jobs.China.catalog2registrationv3reg3.IsContentFlatContainer} ^
    -storageSuffix #{Jobs.Common.China.StorageSuffix} ^
    -cursorUri #{Jobs.China.catalog2registrationv3reg3.CursorUri} ^
    -flatContainerName #{Jobs.China.catalog2registrationv3reg3.FlatContainerName} ^
    -storageOperationMaxExecutionTimeInSeconds #{Jobs.Common.China.AzureOperationMaxTimeout} ^
    -storageServerTimeoutInSeconds #{Jobs.Common.China.StorageServerTimeoutInSeconds} ^
    -galleryBaseAddress #{Jobs.China.catalog2registrationv3reg3.GalleryBaseAddress} ^
    -maxConcurrentBatches #{Jobs.common.v3.MaxConcurrentBatches}

echo "Finished #{Jobs.China.catalog2registrationv3reg3.Title}"

goto Top
