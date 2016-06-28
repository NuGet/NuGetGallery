@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.rollupdownloadfacts.Title}"
	
	title #{Jobs.stats.rollupdownloadfacts.Title}

    start /w stats.rollupdownloadfacts.exe -LogsAzureStorageConnectionString #{Jobs.stats.rollupdownloadfacts.Storage.Primary} -MinAgeInDays "#{Jobs.stats.rollupdownloadfacts.MinAgeInDays}" -StatisticsDatabase "#{Jobs.stats.rollupdownloadfacts.StatisticsDatabase}" -InstrumentationKey "#{Jobs.stats.rollupdownloadfacts.InstrumentationKey}" -verbose true -interval #{Jobs.stats.rollupdownloadfacts.Interval}

	echo "Finished #{Jobs.stats.rollupdownloadfacts.Title}"

	goto Top