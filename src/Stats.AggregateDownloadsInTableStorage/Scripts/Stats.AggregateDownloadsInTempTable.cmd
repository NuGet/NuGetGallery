@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.aggregatedownloadsintemptable.Title}"
	
	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.stats.aggregatedownloadsintemptable.Storage.Primary}

	title #{Jobs.stats.aggregatedownloadsintemptable.Title}

    start /w stats.aggregatedownloadsintemptable.exe -AzureCdnCloudStorageAccount "#{Jobs.stats.aggregatedownloadsintemptable.AzureCdn.CloudStorageAccount}" -verbose true -interval #{Jobs.stats.aggregatedownloadsintemptable.Interval}

	echo "Finished #{Jobs.stats.aggregatedownloadsintemptable.Title}"

	goto Top