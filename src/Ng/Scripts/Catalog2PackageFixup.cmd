@echo OFF

cd Ng

echo "Starting job - #{Jobs.ngcatalog2packagefixup.Title}"

title #{Jobs.ngcatalog2packagefixup.Title}

Ng.exe catalog2packagefixup ^
	-source #{Jobs.ngcatalog2packagefixup.Catalog.Source} ^
	-storageAccountName #{Jobs.ngcatalog2packagefixup.StorageAccount.Name} ^
	-storageKeyValue #{Jobs.ngcatalog2packagefixup.StorageAccount.Key} ^
	-storageContainer #{Jobs.ngcatalog2packagefixup.Container} ^
	-verify #{Jobs.ngcatalog2packagefixup.Verify} ^
	-instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} ^
	-vaultName #{Deployment.Azure.KeyVault.VaultName} ^
	-clientId #{Deployment.Azure.KeyVault.ClientId} ^
	-certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} ^
	-verbose true

echo "Finished #{Jobs.ngcatalog2packagefixup.Title}"