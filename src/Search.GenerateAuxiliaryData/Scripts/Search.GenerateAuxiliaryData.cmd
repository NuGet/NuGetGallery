@echo OFF

cd bin

:Top
    echo "Starting job - #{Jobs.search.generateauxiliarydata.Title}"

    title #{Jobs.search.generateauxiliarydata.Title}

    start /w search.generateauxiliarydata.exe ^
        -Configuration "#{Jobs.search.generateauxiliarydata.Configuration}" ^
        -InstanceName Search.GenerateAuxillaryData-usnc ^
        -verbose true ^
        -Sleep #{Jobs.search.generateauxiliarydata.Sleep} ^
        -InstrumentationKey "#{Jobs.search.generateauxiliarydata.ApplicationInsightsInstrumentationKey}"

    echo "Finished #{Jobs.search.generateauxiliarydata.Title}"

    goto Top