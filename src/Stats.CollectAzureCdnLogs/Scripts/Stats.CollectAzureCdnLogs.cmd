@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.collectazurecdnlogs.Title}"
	
	title #{Jobs.stats.collectazurecdnlogs.Title}

    start /w stats.collectazurecdnlogs.exe  -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -LogsAzureStorageConnectionString #{Jobs.stats.collectazurecdnlogs.Storage.Primary} -FtpSourceUri "#{Jobs.stats.collectazurecdnlogs.FtpSource.Uri}" -FtpSourceUsername "#{Jobs.stats.collectazurecdnlogs.FtpSource.Username}" -FtpSourcePassword "#{Jobs.stats.collectazurecdnlogs.FtpSource.Password}" -AzureCdnAccountNumber "#{Jobs.stats.collectazurecdnlogs.AzureCdn.AccountNumber}" -AzureCdnPlatform "#{Jobs.stats.collectazurecdnlogs.AzureCdn.Platform}" -AzureCdnCloudStorageAccount "#{Jobs.stats.collectazurecdnlogs.AzureCdn.CloudStorageAccount}" -AzureCdnCloudStorageContainerName "#{Jobs.stats.collectazurecdnlogs.AzureCdn.CloudStorageContainerName}" -InstrumentationKey "#{Jobs.stats.collectazurecdnlogs.InstrumentationKey}" -verbose true -interval #{Jobs.stats.collectazurecdnlogs.Interval}

	echo "Finished #{Jobs.stats.collectazurecdnlogs.Title}"

	goto Top