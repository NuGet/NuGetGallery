@echo OFF

cd Ng

:Top
echo "Starting job - #{Jobs.catalog2lucenev3reg2southeastasia.Title}"

title #{Jobs.catalog2lucenev3reg2southeastasia.Title}

start /w Ng.exe catalog2lucene ^
    -instanceName catalog2lucene-sea ^
    -source #{Jobs.common.v3.Source} ^
    -luceneDirectoryType azure ^
    -luceneStorageAccountName #{Jobs.SouthEastAsia.v3.Storage.Name} ^
    -luceneStorageKeyValue #{Jobs.SouthEastAsia.v3.Storage.Key} ^
    -luceneStorageContainer #{Jobs.SouthEastAsia.catalog2lucenev3.LuceneContainer} ^
    -registration #{Jobs.China.catalog2lucenev3reg2.Registration} ^
    -commitTimeoutInSeconds #{Jobs.catalog2lucene.CommitTimeoutInSeconds} ^
    -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
    -vaultName #{Deployment.Azure.KeyVault.VaultName} ^
    -clientId #{Deployment.Azure.KeyVault.ClientId} ^
    -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
    -verbose true -interval #{Jobs.common.v3.Interval} ^
    -galleryBaseAddress #{Jobs.common.v3.GalleryBaseAddress}

echo "Finished #{Jobs.catalog2lucenev3reg2southeastasia.Title}"

goto Top