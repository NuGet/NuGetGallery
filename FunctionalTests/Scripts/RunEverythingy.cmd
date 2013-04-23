Set GalleryUrl=http://nugetgallery-qa.cloudapp.net/
"%VS110COMNTOOLS%..\IDE\mstest.exe"  /testsettings:"..\Local.testsettings" /testmetadata:"..\NuGetGallery.FunctionalTests.vsmdi"