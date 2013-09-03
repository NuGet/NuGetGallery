Set GalleryUrl = %1
IF "%GalleryUrl%"=="" (
Set GalleryUrl=http://nugetgallery-bvts.cloudapp.net/
) 
"%VS110COMNTOOLS%..\IDE\mstest.exe"  /testsettings:"..\Local.testsettings" /testmetadata:"..\NuGetGallery.FunctionalTests.vsmdi" /testlist:AllHappyPathTests