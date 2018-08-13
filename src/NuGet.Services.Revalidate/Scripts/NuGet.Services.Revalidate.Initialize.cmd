@echo OFF

cd bin

:Top
echo "Initializing job - #{Jobs.nuget.services.revalidate.Title}"

title #{Jobs.nuget.services.revalidate.Title}

start /w NuGet.Services.Revalidate.exe ^
    -Configuration #{Jobs.nuget.services.revalidate.Configuration} ^
    -InstrumentationKey "#{Jobs.nuget.services.revalidate.InstrumentationKey}" ^
    -Initialize ^
    -VerifyInitialization

echo "Initialized #{Jobs.nuget.services.revalidate.Title}"
