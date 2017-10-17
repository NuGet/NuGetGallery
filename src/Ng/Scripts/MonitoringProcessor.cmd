@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.monitoringprocessor.Title}"

	title #{Jobs.monitoringprocessor.Title}
    
	start /w .\Ng.exe monitoringprocessor -source #{Jobs.common.v3.Source} -index #{Jobs.common.v3.index} -gallery #{Jobs.common.v3.f2c.Gallery} -endpointsToTest "#{Jobs.endpointmonitoring.EndpointsToTest}" -statusFolder #{Jobs.endpointmonitoring.StatusFolder} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.endpointmonitoring.MonitoringContainer} -storageQueueName #{Jobs.endpointmonitoring.PackageValidatorQueue} -storageTypeAuditing azure -storageAccountNameAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountName} -storageKeyValueAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountKey} -storageContainerAuditing auditing -storagePathAuditing package -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.monitoringprocessor.Title}"

	goto Top