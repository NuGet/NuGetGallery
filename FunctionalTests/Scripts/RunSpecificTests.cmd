set GalleryUrl=%1
set criteria=%2
"%VS110COMNTOOLS%..\IDE\mstest.exe" /testcontainer:"..\NuGetGallery.FunctionalTests\bin\Debug\NuGetGallery.FunctionalTests.dll" /testsettings:"..\Local.testsettings" /test:%2