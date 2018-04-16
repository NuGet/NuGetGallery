@echo OFF

cd bin

:Top
echo "Starting job - #{Jobs.validation.packagesigning.revalidatecertificate.Title}"

title #{Jobs.validation.packagesigning.revalidatecertificate.Title}

start /w Validation.PackageSigning.RevalidateCertificate.exe ^
    -Configuration #{Jobs.validation.packagesigning.revalidatecertificate.Configuration} ^
    -InstrumentationKey "#{Jobs.validation.packagesigning.revalidatecertificate.InstrumentationKey}" ^
    -ReinitializeAfterSeconds 86400

echo "Finished #{Jobs.validation.packagesigning.revalidatecertificate.Title}"

goto Top
