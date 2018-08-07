@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.archivepackages.Title}"

	title #{Jobs.archivepackages.Title}

	start /w archivepackages.exe -Configuration "#{Jobs.archivepackages.Configuration}" -Sleep "#{Jobs.archivepackages.Sleep}" -InstrumentationKey "#{Jobs.archivepackages.ApplicationInsightsInstrumentationKey}"

	echo "Finished #{Jobs.archivepackages.Title}"

	goto Top
	