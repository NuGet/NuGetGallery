@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.monitoring2monitoring.Title}"

	title #{Jobs.monitoring2monitoring.Title}
    
	start /w .\Ng.exe monitoring2monitoring -statusFolder #{Jobs.endpointmonitoring.StatusFolder} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} -storageQueueName #{Jobs.endpointmonitoring.PackageValidatorQueue} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.monitoring2monitoring.Interval}

	echo "Finished #{Jobs.monitoring2monitoring.Title}"

	goto Top