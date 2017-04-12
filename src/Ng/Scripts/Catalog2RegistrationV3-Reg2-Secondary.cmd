@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2registrationv3reg2secondary.Title}"

title #{Jobs.catalog2registrationv3reg2secondary.Title}

start /w ng.exe catalog2registration ^
    -source #{Jobs.catalog2registrationv3reg2secondary.Source} ^
    -contentBaseAddress #{Jobs.catalog2registrationv3reg2secondary.ContentBaseAddress} ^
    -storageType azure ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -storageBaseAddress #{Jobs.catalog2registrationv3reg2secondary.StorageBaseAddress} ^
    -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -storageContainer #{Jobs.catalog2registrationv3reg2secondary.StorageContainer} ^
    -useCompressedStorage #{Jobs.catalog2registrationv3reg2secondary.UseCompressedStorage} ^
    -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg2secondary.StorageBaseAddressCompressed} ^
    -compressedStorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -compressedStorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -compressedStorageContainer #{Jobs.catalog2registrationv3reg2secondary.StorageContainerCompressed} ^
    -useSemVer2Storage #{Jobs.catalog2registrationv3reg2secondary.UseSemVer2Storage} ^
    -semVer2StorageBaseAddress #{Jobs.catalog2registrationv3reg2secondary.StorageBaseAddressSemVer2} ^
    -semVer2StorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -semVer2StorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -semVer2StorageContainer #{Jobs.catalog2registrationv3reg2secondary.StorageContainerSemVer2}

echo "Finished #{Jobs.catalog2registrationv3reg2secondary.Title}"

goto Top
