@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2lucenev3reg2ussc.Title}"

title #{Jobs.catalog2lucenev3reg2ussc.Title}

start /w ng.exe catalog2lucene ^
    -instanceName catalog2lucene-ussc ^
    -source #{Jobs.common.v3.Source} ^
    -luceneDirectoryType azure ^
    -luceneStorageAccountName #{Jobs.common.v3.Storage.USSC.Name} ^
    -luceneStorageKeyValue #{Jobs.common.v3.Storage.USSC.Key} ^
    -luceneStorageContainer #{Jobs.catalog2lucenev3reg2ussc.LuceneContainer} ^
    -registration #{Jobs.catalog2lucenev3reg2ussc.Registration} ^
    -commitTimeoutInSeconds #{Jobs.catalog2lucene.CommitTimeoutInSeconds} ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true ^
    -interval #{Jobs.common.v3.Interval} ^
    -galleryBaseAddress #{Jobs.common.v3.GalleryBaseAddress}

echo "Finished #{Jobs.catalog2lucenev3reg2ussc.Title}"

goto Top
