@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.stats.importazurecdnstatistics.Title}"
	
title #{Jobs.stats.importazurecdnstatistics.Title}

start /w stats.importazurecdnstatistics.exe ^
    -Configuration "#{Jobs.stats.importazurecdnstatistics.Configuration}" ^
    -InstrumentationKey "#{Jobs.stats.importazurecdnstatistics.InstrumentationKey}" ^
    -Interval #{Jobs.stats.importazurecdnstatistics.Interval} ^
    -verbose true

echo "Finished #{Jobs.stats.importazurecdnstatistics.Title}"

goto Top