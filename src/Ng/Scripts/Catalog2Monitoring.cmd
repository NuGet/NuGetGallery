@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2monitoring.Title}"

	title #{Jobs.catalog2monitoring.Title}
    
	start /w .\Ng.exe catalog2monitoring -source #{Jobs.common.v3.Source} -index #{Jobs.common.v3.index} -gallery #{Jobs.common.v3.f2c.Gallery} -endpointsToTest "#{Jobs.endpointmonitoring.EndpointsToTest}" -statusFolder #{Jobs.endpointmonitoring.StatusFolder} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} -storageQueueName #{Jobs.endpointmonitoring.PackageValidatorQueue} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2monitoring.Title}"

	goto Top