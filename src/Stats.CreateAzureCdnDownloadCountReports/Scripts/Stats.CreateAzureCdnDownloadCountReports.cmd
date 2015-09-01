@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.createazurecdndownloadcountreports.Title}"
	
	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.stats.createazurecdndownloadcountreports.Storage.Primary}

	title #{Jobs.stats.createazurecdndownloadcountreports.Title}

	start /w stats.createazurecdndownloadcountreports.exe -AzureCdnCloudStorageAccount "#{Jobs.stats.createazurecdndownloadcountreports.AzureCdn.CloudStorageAccount}" -AzureCdnCloudStorageContainerName "#{Jobs.stats.createazurecdndownloadcountreports.AzureCdn.CloudStorageContainerName}" -StatisticsDatabase "#{Jobs.stats.createazurecdndownloadcountreports.StatisticsDatabase}" -SourceDatabase "#{Jobs.stats.createazurecdndownloadcountreports.SourceDatabase}" -verbose true -interval #{Jobs.stats.createazurecdndownloadcountreports.Interval}

	echo "Finished #{Jobs.stats.createazurecdndownloadcountreports.Title}"

	goto Top