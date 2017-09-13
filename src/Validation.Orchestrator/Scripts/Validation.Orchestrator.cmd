@echo OFF
    
cd bin

:Top
    echo "Starting job - #{Jobs.validation.Title}"
    
    title #{Jobs.validation.Title}

    start /w Validation.Orchestrator.exe

    echo "Finished #{Jobs.validation.Title}"

    goto Top
