@echo OFF

REM This script is the same as Search.GenerateAuxillaryData.cmd. However, this copy is required until "Jobs.ServiceNames" deployment config is consolidated.

cd bin

:Top
	echo "Starting job - #{Jobs.search.generateauxiliarydata.Title}"

	title #{Jobs.search.generateauxiliarydata.Title}

    start /w search.generateauxiliarydata.exe -Configuration "#{Jobs.search.generateauxiliarydata.Configuration}" -verbose true -Sleep #{Jobs.search.generateauxiliarydata.Sleep} -InstrumentationKey "#{Jobs.search.generateauxiliarydata.ApplicationInsightsInstrumentationKey}"
	
	echo "Finished #{Jobs.search.generateauxiliarydata.Title}"

	goto Top