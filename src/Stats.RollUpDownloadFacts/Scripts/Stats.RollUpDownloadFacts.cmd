@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.rollupdownloadfacts.Title}"
	
	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.stats.rollupdownloadfacts.Storage.Primary}

	title #{Jobs.stats.rollupdownloadfacts.Title}

    start /w stats.rollupdownloadfacts.exe -MinAgeInDays "#{Jobs.stats.rollupdownloadfacts.MinAgeInDays}" -verbose true -interval #{Jobs.stats.rollupdownloadfacts.Interval}

	echo "Finished #{Jobs.stats.rollupdownloadfacts.Title}"

	goto Top