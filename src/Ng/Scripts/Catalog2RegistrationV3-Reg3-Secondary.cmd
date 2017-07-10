@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2registrationv3reg3secondary.Title}"

title #{Jobs.catalog2registrationv3reg3secondary.Title}

start /w ng.exe catalog2registration ^
    -source #{Jobs.catalog2registrationv3reg3secondary.Source} ^
    -contentBaseAddress #{Jobs.catalog2registrationv3reg3secondary.ContentBaseAddress} ^
    -storageType azure ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -storageBaseAddress #{Jobs.catalog2registrationv3reg3secondary.StorageBaseAddress} ^
    -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -storageContainer #{Jobs.catalog2registrationv3reg3secondary.StorageContainer} ^
    -useCompressedStorage #{Jobs.catalog2registrationv3reg3secondary.UseCompressedStorage} ^
    -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg3secondary.StorageBaseAddressCompressed} ^
    -compressedStorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -compressedStorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -compressedStorageContainer #{Jobs.catalog2registrationv3reg3secondary.StorageContainerCompressed} ^
    -useSemVer2Storage #{Jobs.catalog2registrationv3reg3secondary.UseSemVer2Storage} ^
    -semVer2StorageBaseAddress #{Jobs.catalog2registrationv3reg3secondary.StorageBaseAddressSemVer2} ^
    -semVer2StorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -semVer2StorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -semVer2StorageContainer #{Jobs.catalog2registrationv3reg3secondary.StorageContainerSemVer2} ^
    -contentIsFlatContainer #{Jobs.catalog2registrationv3reg3.IsContentFlatContainer} ^
    -cursorUri #{Jobs.catalog2registrationv3reg3.CursorUri} ^
    -flatContainerName #{Jobs.catalog2registrationv3reg3.FlatContainerName}

echo "Finished #{Jobs.catalog2registrationv3reg3secondary.Title}"

goto Top
