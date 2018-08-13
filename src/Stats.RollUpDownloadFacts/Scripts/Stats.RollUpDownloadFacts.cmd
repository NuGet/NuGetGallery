@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.stats.rollupdownloadfacts.Title}"
	
title #{Jobs.stats.rollupdownloadfacts.Title}

start /w stats.rollupdownloadfacts.exe ^
    -Configuration "#{Jobs.stats.rollupdownloadfacts.Configuration}" ^
    -InstrumentationKey "#{Jobs.stats.rollupdownloadfacts.InstrumentationKey}" ^
    -Interval #{Jobs.stats.rollupdownloadfacts.Interval} ^
    -verbose true

echo "Finished #{Jobs.stats.rollupdownloadfacts.Title}"

goto Top