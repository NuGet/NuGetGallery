@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.updatelicensereports.Title}"
	
title #{Jobs.updatelicensereports.Title}

start /w updatelicensereports.exe -Configuration #{Jobs.updatelicensereports.Configuration}  ^
    -verbose true  ^
    -Sleep #{Jobs.updatelicensereports.Sleep}  ^
    -InstrumentationKey "#{Jobs.updatelicensereports.ApplicationInsightsInstrumentationKey}"

echo "Finished #{Jobs.updatelicensereports.Title}"

goto Top