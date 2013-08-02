Set Criteria = %1
Set GalleryUrl = %2

IF "%Criteria%" == "" (
ECHO "Specify a search string to filter tests. Example :RunSpecificTests.cmd Download. You can optionally specify the environment that you want to point to as second parameter."
exit /b
)
IF "%GalleryUrl%"=="" (
Set GalleryUrl=http://nugetgallery-bvts.cloudapp.net/
) 

"%VS110COMNTOOLS%..\IDE\mstest.exe" /testcontainer:"..\NuGetGallery.FunctionalTests\bin\Debug\NuGetGallery.FunctionalTests.dll" /testsettings:"..\Local.testsettings" /test:"%Criteria%"
