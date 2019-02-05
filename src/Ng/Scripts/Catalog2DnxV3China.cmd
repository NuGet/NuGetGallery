@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.ngcatalog2dnxChina.Title}"

title #{Jobs.ngcatalog2dnxChina.Title}

start /w Ng.exe catalog2dnx ^
    -instanceName catalog2dnx-china ^
    -source #{Jobs.ngcatalog2dnx.Catalog.Source} ^
    -contentBaseAddress #{Jobs.China.ngcatalog2dnx.ContentBaseAddress} ^
    -storageBaseAddress #{Jobs.China.ngcatalog2dnx.StorageBaseAddress} ^
    -storageType azure ^
    -storageAccountName #{Jobs.common.China.v3.StorageAccountName} ^
    -storageKeyValue #{Jobs.common.China.v3.StorageKey} ^
    -storageContainer #{Jobs.ngcatalog2dnx.Container} ^
    -preferAlternatePackageSourceStorage #{Jobs.ngcatalog2dnx.PreferAlternatePackageSourceStorage} ^
    -storageAccountNamePreferredPackageSourceStorage #{Jobs.ngcatalog2dnx.PreferredPackageSourceStorageAccountName} ^
    -storageKeyValuePreferredPackageSourceStorage #{Jobs.ngcatalog2dnx.PreferredPackageSourceStorageAccountKey} ^
    -storageContainerPreferredPackageSourceStorage #{Jobs.ngcatalog2dnx.PreferredPackageSourceStorageContainerName} ^
    -storageUseServerSideCopy #{Jobs.China.ngcatalog2dnx.StorageUseServerSideCopy} ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.ngcatalog2dnx.Interval} ^
    -storageSuffix #{Jobs.Common.China.StorageSuffix} ^
    -storageOperationMaxExecutionTimeInSeconds #{Jobs.Common.China.AzureOperationMaxTimeout} ^
    -httpClientTimeoutInSeconds #{Jobs.ngcatalog2dnx.HttpClientTimeoutInSeconds}

echo "Finished #{Jobs.ngcatalog2dnxChina.Title}"

goto Top
