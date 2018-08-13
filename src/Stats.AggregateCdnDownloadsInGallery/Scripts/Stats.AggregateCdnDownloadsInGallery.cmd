@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.stats.aggregatecdndownloadsingallery.Title}"
	
title #{Jobs.stats.aggregatecdndownloadsingallery.Title}

start /w stats.aggregatecdndownloadsingallery.exe ^
	-Configuration "#{Jobs.stats.aggregatecdndownloadsingallery.Configuration}" ^
	-InstrumentationKey "#{Jobs.stats.aggregatecdndownloadsingallery.InstrumentationKey}" ^
	-verbose true ^
	-Interval #{Jobs.stats.aggregatecdndownloadsingallery.Interval}

echo "Finished #{Jobs.stats.aggregatecdndownloadsingallery.Title}"

goto Top
