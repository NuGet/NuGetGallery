set GalleryUrl=%1
mstest /testcontainer:"..\NuGetGallery.FunctionalTests\bin\NuGetGallery.FunctionalTests.dll" /testsettings:"..\Local.testsettings" 