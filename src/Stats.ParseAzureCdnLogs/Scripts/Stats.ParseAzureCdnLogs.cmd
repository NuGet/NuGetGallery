@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.parseazurecdnlogs.Title}"
	
	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.stats.parseazurecdnlogs.Storage.Primary}

	title #{Jobs.stats.parseazurecdnlogs.Title}

    start /w stats.parseazurecdnlogs.exe -AzureCdnAccountNumber "#{Jobs.stats.parseazurecdnlogs.AzureCdn.AccountNumber}" -AzureCdnPlatform "#{Jobs.stats.parseazurecdnlogs.AzureCdn.Platform}" -AzureCdnCloudStorageAccount "#{Jobs.stats.parseazurecdnlogs.AzureCdn.CloudStorageAccount}" -AzureCdnCloudStorageContainerName "#{Jobs.stats.parseazurecdnlogs.AzureCdn.CloudStorageContainerName}" -AzureCdnCloudStorageTableName "#{Jobs.stats.parseazurecdnlogs.AzureCdn.CloudStorageTableName}" -verbose true -interval #{Jobs.stats.parseazurecdnlogs.Interval}

	echo "Finished #{Jobs.stats.parseazurecdnlogs.Title}"

	goto Top