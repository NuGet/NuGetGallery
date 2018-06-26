@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.nuget.services.revalidate.Title}"

title #{Jobs.nuget.services.revalidate.Title}

start /w NuGet.Services.Revalidate.exe ^
    -Configuration #{Jobs.nuget.services.revalidate.Configuration} ^
    -InstrumentationKey "#{Jobs.nuget.services.revalidate.InstrumentationKey}"

echo "Finished #{Jobs.nuget.services.revalidate.Title}"

goto Top
