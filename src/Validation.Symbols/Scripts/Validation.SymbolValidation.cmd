@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.Validation.Symbols.Title}"

title #{Jobs.Validation.Symbols.Title}

start /w Validation.Symbols.Job.exe ^
    -Configuration #{Jobs.validation.SymbolValidation.Configuration} ^
    -InstrumentationKey "#{Jobs.validation.SymbolValidation.InstrumentationKey}"

echo "Finished #{Jobs.Validation.Symbols.Title}"

goto Top
