@echo OFF

REM This script is the same as Search.GenerateAuxillaryData.cmd. However, this copy is required until "Jobs.ServiceNames" deployment config is consolidated.

cd bin

:Top
    echo "Starting job - #{Jobs.SouthEastAsia.search.generateauxiliarydata.Title}"

    title #{Jobs.SouthEastAsia.search.generateauxiliarydata.Title}

    start /w search.generateauxiliarydata.exe ^
        -Configuration "#{Jobs.SouthEastAsia.search.generateauxiliarydata.Configuration}" ^
        -InstanceName Search.GenerateAuxillaryData-sea ^
        -verbose true ^
        -Sleep #{Jobs.search.generateauxiliarydata.Sleep} ^
        -InstrumentationKey "#{Jobs.search.generateauxiliarydata.ApplicationInsightsInstrumentationKey}"

    echo "Finished #{Jobs.SouthEastAsia.search.generateauxiliarydata.Title}"

    goto Top