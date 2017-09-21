@echo OFF
	
cd bin

:Top
    echo "Starting job - #{Jobs.Stats.CollectAzureChinaCDNLogs.Title}"

    title #{Jobs.Stats.CollectAzureChinaCDNLogs.Title}

    start /w Stats.CollectAzureChinaCDNLogs.exe ^
    -VaultName "#{Deployment.Azure.KeyVault.VaultName}" ^
    -ClientId "#{Deployment.Azure.KeyVault.ClientId}" ^
    -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" ^
    -AzureAccountConnectionStringSource "#{Jobs.Stats.CollectAzureChinaCDNLogs.AzureAccountConnectionStringSource}" ^
    -AzureAccountConnectionStringDestination "#{Jobs.Stats.CollectAzureChinaCDNLogs.AzureAccountConnectionStringDestination}" ^
    -AzureContainerNameSource "#{Jobs.Stats.CollectAzureChinaCDNLogs.AzureContainerNameSource}" ^
    -AzureContainerNameDestination "#{Jobs.Stats.CollectAzureChinaCDNLogs.AzureContainerNameDestination}" ^
    -DestinationFilePrefix "#{Jobs.Stats.CollectAzureChinaCDNLogs.DestinationFilePrefix}" ^
    -ExecutionTimeoutInSeconds "#{Jobs.Stats.CollectAzureChinaCDNLogs.ExecutionTimeoutInSeconds}" ^
    -InstrumentationKey "#{Jobs.Stats.CollectAzureChinaCDNLogs.InstrumentationKey}" ^
    -verbose true ^
    -Interval #{Jobs.Stats.CollectAzureChinaCDNLogs.Interval}

    echo "Finished #{Jobs.Stats.CollectAzureChinaCDNLogs.Title}"

    goto Top