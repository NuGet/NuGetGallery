@echo OFF

cd bin

echo "Starting job - #{Jobs.backupv3storage.Title}"

title #{Jobs.backupv3storage.Title}

start /w CopyAzureContainer.exe ^
    -SourceContainerInfo_lucene #{Jobs.common.v3.Storage.Primary.Name}:#{Jobs.common.v3.Storage.Primary.Key}:#{Jobs.catalog2lucenev3reg2.LuceneContainer} ^
    -SourceContainerInfo_catalog #{Jobs.common.v3.c2r.StorageAccountName}:#{Jobs.common.v3.c2r.StorageAccountKey}:#{Jobs.feed2catalogv3.StorageContainer} ^
    -SourceContainerInfo_registration #{Jobs.common.v3.c2r.StorageAccountName}:#{Jobs.common.v3.c2r.StorageAccountKey}:#{Jobs.catalog2registrationv3reg3.StorageContainer} ^
    -SourceContainerInfo_registrationgz #{Jobs.common.v3.c2r.StorageAccountName}:#{Jobs.common.v3.c2r.StorageAccountKey}:#{Jobs.catalog2registrationv3reg3.StorageContainerCompressed} ^
    -SourceContainerInfo_registrationsemver2 #{Jobs.common.v3.c2r.StorageAccountName}:#{Jobs.common.v3.c2r.StorageAccountKey}:#{Jobs.catalog2registrationv3reg3.StorageContainerSemVer2} ^
    -DestStorageAccountName #{Jobs.backupv3storage.destStorageAccountName} ^
    -DestStorageKeyValue #{Jobs.backupv3storage.destStorageKeyValue} ^
    -BackupDays #{Jobs.backupv3storage.backupDays} ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -Once

    echo "Finished #{Jobs.backupv3storage.Title}"

