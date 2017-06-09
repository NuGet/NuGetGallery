@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.endpointmonitoring.Title}"

	title #{Jobs.endpointmonitoring.Title}
    
	start /w .\Ng.exe endpointmonitoring -source #{Jobs.common.v3.Source} -index #{Jobs.common.v3.index} -gallery #{Jobs.common.v3.f2c.Gallery} -endpointsToTest "#{Jobs.endpointmonitoring.EndpointsToTest}" -statusFolder #{Jobs.endpointmonitoring.StatusFolder} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} -storageTypeAuditing azure -storageAccountNameAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountName} -storageKeyValueAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountKey} -storageContainerAuditing auditing -storagePathAuditing package -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.endpointmonitoring.Title}"

	goto Top
	