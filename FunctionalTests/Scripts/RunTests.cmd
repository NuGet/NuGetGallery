set GalleryUrl=%1
"%VS110COMNTOOLS%..\IDE\mstest.exe" /testcontainer:"..\NuGetGallery.FunctionalTests\bin\NuGetGallery.FunctionalTests.dll" /testsettings:"..\Local.testsettings" 