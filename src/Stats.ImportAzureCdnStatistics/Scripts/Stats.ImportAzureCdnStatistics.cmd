@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.importazurecdnstatistics.Title}"
	
	title #{Jobs.stats.importazurecdnstatistics.Title}

	start /w stats.importazurecdnstatistics.exe -LogsAzureStorageConnectionString #{Jobs.stats.importazurecdnstatistics.Storage.Primary} -AzureCdnCloudStorageAccount "#{Jobs.stats.importazurecdnstatistics.AzureCdn.CloudStorageAccount}" -AzureCdnCloudStorageContainerName "#{Jobs.stats.importazurecdnstatistics.AzureCdn.CloudStorageContainerName}" -AzureCdnPlatform "#{Jobs.stats.importazurecdnstatistics.AzureCdn.Platform}" -AzureCdnAccountNumber "#{Jobs.stats.importazurecdnstatistics.AzureCdn.AccountNumber}" -StatisticsDatabase "#{Jobs.stats.importazurecdnstatistics.StatisticsDatabase}" -InstrumentationKey "#{Jobs.stats.importazurecdnstatistics.InstrumentationKey}" -verbose true -interval #{Jobs.stats.importazurecdnstatistics.Interval}

	echo "Finished #{Jobs.stats.importazurecdnstatistics.Title}"

	goto Top