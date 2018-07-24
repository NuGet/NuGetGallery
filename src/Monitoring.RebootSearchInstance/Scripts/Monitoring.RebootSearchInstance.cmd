@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.Monitoring.RebootSearchInstance.Title}"

title #{Jobs.Monitoring.RebootSearchInstance.Title}

start /w NuGet.Monitoring.RebootSearchInstance.exe ^
    -Configuration #{Jobs.Monitoring.RebootSearchInstance.Configuration} ^
    -InstrumentationKey "#{Jobs.Monitoring.RebootSearchInstance.InstrumentationKey}"

echo "Finished #{Jobs.Monitoring.RebootSearchInstance.Title}"

goto Top
