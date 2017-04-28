@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2lucenev3reg2.Title}"

title #{Jobs.catalog2lucenev3reg2.Title}

start /w ng.exe catalog2lucene ^
    -source #{Jobs.common.v3.Source} ^
    -luceneDirectoryType azure ^
    -luceneStorageAccountName #{Jobs.common.v3.Storage.Primary.Name} ^
    -luceneStorageKeyValue #{Jobs.common.v3.Storage.Primary.Key} ^
    -luceneStorageContainer #{Jobs.catalog2lucenev3reg2.LuceneContainer} ^
    -registration #{Jobs.catalog2lucenev3reg2.Registration} ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval}

echo "Finished #{Jobs.catalog2lucenev3reg2.Title}"

goto Top
