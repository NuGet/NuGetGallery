@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.stats.collectazurecdnlogs.Title}"
	
title #{Jobs.stats.collectazurecdnlogs.Title}

start /w stats.collectazurecdnlogs.exe ^
    -Configuration "#{Jobs.stats.collectazurecdnlogs.Configuration}" ^
    -InstrumentationKey "#{Jobs.stats.collectazurecdnlogs.InstrumentationKey}" ^
    -verbose true ^
    -Interval #{Jobs.stats.collectazurecdnlogs.Interval}

echo "Finished #{Jobs.stats.collectazurecdnlogs.Title}"

goto Top