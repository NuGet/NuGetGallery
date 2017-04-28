@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2registrationv3reg2.Title}"

title #{Jobs.catalog2registrationv3reg2.Title}

start /w ng.exe catalog2registration ^
    -source #{Jobs.common.v3.Source} ^
    -contentBaseAddress #{Jobs.catalog2registrationv3reg2.ContentBaseAddress} ^
    -storageType azure ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -storageBaseAddress #{Jobs.catalog2registrationv3reg2.StorageBaseAddress} ^
    -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} ^
    -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
    -storageContainer #{Jobs.catalog2registrationv3reg2.StorageContainer} ^
    -useCompressedStorage #{Jobs.catalog2registrationv3reg2.UseCompressedStorage} ^
    -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg2.StorageBaseAddressCompressed} ^
    -compressedStorageAccountName #{Jobs.common.v3.c2r.StorageAccountName} ^
    -compressedStorageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
    -compressedStorageContainer #{Jobs.catalog2registrationv3reg2.StorageContainerCompressed} ^
    -useSemVer2Storage #{Jobs.catalog2registrationv3reg2.UseSemVer2Storage} ^
    -semVer2StorageBaseAddress #{Jobs.catalog2registrationv3reg2.StorageBaseAddressSemVer2} ^
    -semVer2StorageAccountName #{Jobs.common.v3.c2r.StorageAccountName} ^
    -semVer2StorageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
    -semVer2StorageContainer #{Jobs.catalog2registrationv3reg2.StorageContainerSemVer2}

echo "Finished #{Jobs.catalog2registrationv3reg2.Title}"

goto Top
