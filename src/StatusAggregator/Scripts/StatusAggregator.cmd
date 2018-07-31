@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.statusaggregator.Title}"

title #{Jobs.archivepackages.Title}

start /w statusaggregator.exe ^
	-StatusIncidentApiBaseUri "#{Jobs.statusaggregator.IncidentApiBaseUri}" ^
    -StatusIncidentApiTeamId "#{Jobs.statusaggregator.IncidentApiTeamId}" ^
    -StatusIncidentApiCertificate "#{Jobs.statusaggregator.IncidentApiCertificate}" ^
    -StatusStorageAccount "#{Jobs.statusaggregator.StorageAccount}" ^
    -StatusContainerName "#{Jobs.statusaggregator.ContainerName}" ^
    -StatusTableName "#{Jobs.statusaggregator.TableName}" ^
    -StatusEnvironment "#{Jobs.statusaggregator.Environment}" ^
    -StatusMaximumSeverity "#{Jobs.statusaggregator.MaximumSeverity}" ^
    -VaultName "#{Deployment.Azure.KeyVault.VaultName}" ^
    -ClientId "#{Deployment.Azure.KeyVault.ClientId}" ^
    -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" ^
    -Sleep "#{Jobs.statusaggregator.Sleep}" ^
    -InstrumentationKey "#{Jobs.statusaggregator.ApplicationInsightsInstrumentationKey}"

echo "Finished #{Jobs.statusaggregator.Title}"

goto Top
	