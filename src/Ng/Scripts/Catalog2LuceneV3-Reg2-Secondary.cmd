@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2lucenev3reg2secondary.Title}"

title #{Jobs.catalog2lucenev3reg2secondary.Title}

start /w ng.exe catalog2lucene ^
    -source #{Jobs.catalog2lucenev3reg2secondary.Source} ^
    -luceneDirectoryType azure ^
    -luceneStorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} ^
    -luceneStorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} ^
    -luceneStorageContainer #{Jobs.catalog2lucenev3reg2secondary.LuceneContainer} ^
    -registration #{Jobs.catalog2lucenev3reg2secondary.Registration} ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval}

echo "Finished #{Jobs.catalog2lucenev3reg2secondary.Title}"

goto Top
