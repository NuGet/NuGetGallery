@echo OFF
    
cd bin

:Top
    echo "Starting job - #{Jobs.validation.Title}"
    
    title #{Jobs.validation.Title}

    start /w NuGet.Services.Validation.Orchestrator.exe -Configuration #{Jobs.validation.configuration} -InstrumentationKey #{Jobs.validation.ApplicationInsightsInstrumentationKey}

    echo "Finished #{Jobs.validation.Title}"

    goto Top
