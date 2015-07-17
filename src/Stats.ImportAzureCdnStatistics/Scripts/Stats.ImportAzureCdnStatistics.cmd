@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.importazurecdnstatistics.Title}"
	
	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.stats.importazurecdnstatistics.Storage.Primary}

	title #{Jobs.stats.importazurecdnstatistics.Title}

    start /w stats.importazurecdnstatistics.exe -AzureCdnCloudStorageAccount "#{Jobs.stats.importazurecdnstatistics.AzureCdn.CloudStorageAccount}" -StatisticsDatabase "#{Jobs.stats.importazurecdnstatistics.StatisticsDatabase}" -verbose true -interval #{Jobs.stats.importazurecdnstatistics.Interval}

	echo "Finished #{Jobs.stats.importazurecdnstatistics.Title}"

	goto Top