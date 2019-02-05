@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2registrationv3reg3.Title}"

title #{Jobs.catalog2registrationv3reg3.Title}

start /w ng.exe catalog2registration ^
    -instanceName catalog2registration-global ^
    -source #{Jobs.catalog2registrationv3reg3.Source} ^
    -contentBaseAddress #{Jobs.catalog2registrationv3reg3.ContentBaseAddress} ^
    -storageType azure ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -storageBaseAddress #{Jobs.catalog2registrationv3reg3.StorageBaseAddress} ^
    -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} ^
    -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
    -storageContainer #{Jobs.catalog2registrationv3reg3.StorageContainer} ^
    -useCompressedStorage #{Jobs.catalog2registrationv3reg3.UseCompressedStorage} ^
    -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg3.StorageBaseAddressCompressed} ^
    -compressedStorageAccountName #{Jobs.common.v3.c2r.StorageAccountName} ^
    -compressedStorageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
    -compressedStorageContainer #{Jobs.catalog2registrationv3reg3.StorageContainerCompressed} ^
    -useSemVer2Storage #{Jobs.catalog2registrationv3reg3.UseSemVer2Storage} ^
    -semVer2StorageBaseAddress #{Jobs.catalog2registrationv3reg3.StorageBaseAddressSemVer2} ^
    -semVer2StorageAccountName #{Jobs.common.v3.c2r.StorageAccountName} ^
    -semVer2StorageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} ^
    -semVer2StorageContainer #{Jobs.catalog2registrationv3reg3.StorageContainerSemVer2} ^
    -contentIsFlatContainer #{Jobs.catalog2registrationv3reg3.IsContentFlatContainer} ^
    -cursorUri #{Jobs.catalog2registrationv3reg3.CursorUri} ^
    -flatContainerName #{Jobs.catalog2registrationv3reg3.FlatContainerName} ^
    -galleryBaseAddress #{Jobs.catalog2registrationv3reg3.GalleryBaseAddress}

echo "Finished #{Jobs.catalog2registrationv3reg3.Title}"

goto Top
