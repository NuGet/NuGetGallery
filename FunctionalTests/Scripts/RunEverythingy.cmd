Set GalleryUrl=http://qa.nugettest.org/
"%VS110COMNTOOLS%..\IDE\mstest.exe"  /testsettings:"..\Local.testsettings" /testmetadata:"..\NuGetGallery.FunctionalTests.vsmdi"