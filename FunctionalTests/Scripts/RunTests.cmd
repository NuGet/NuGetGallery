set GalleryUrl=%1
"%VS110COMNTOOLS%..\IDE\mstest.exe"  /testsettings:"..\Local.testsettings" /testmetadata:"..\NuGetGallery.FunctionalTests.vsmdi" /testlist:AllHappyPathTests