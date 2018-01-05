@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.validation.packagesigning.validatecertificate.Title}"

title #{Jobs.validation.packagesigning.validatecertificate.Title}

start /w Validation.PackageSigning.ValidateCertificate.exe ^
    -Configuration #{Jobs.validation.packagesigning.validatecertificate.Configuration} ^
	-InstrumentationKey "#{Jobs.validation.packagesigning.validatecertificate.InstrumentationKey}"

echo "Finished #{Jobs.validation.packagesigning.validatecertificate.Title}"

goto Top
