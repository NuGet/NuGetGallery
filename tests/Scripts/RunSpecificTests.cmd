@echo off

Set Criteria=%1
Set GalleryUrl=%2

IF "%Criteria%" == "" (
ECHO "Specify a search string to filter tests. Example :RunSpecificTests.cmd Download. You can optionally specify the environment that you want to point to as second parameter."
exit /b
)

REM If GalleryUrl is still not defined, the default is to use int.nugettest.org
if "%GalleryUrl%"=="" (
ECHO Setting GalleryUrl to the default - int.nugettest.org
SET GalleryUrl=https://int.nugettest.org/
)
ECHO The NuGet gallery tests are running against %GalleryUrl%

If Exist ""%VS120COMNTOOLS%"\..\IDE\mstest.exe" (
   set toolpath=%VS120COMNTOOLS%
   set VisualStudioVersion=12.0
   goto Run
)

If Exist ""%VS110COMNTOOLS%"..\IDE\mstest.exe" (
   set toolpath=%VS110COMNTOOLS%
   set VisualStudioVersion=11.0
   goto Run
)

:Error
Echo The variable toolpath is not set correctly. check your visual studio install. Exiting without running tests... 
goto End

:Run
Echo Start running all NuGet Gallery Functional tests...
Echo The path to mstest.exe is "%toolpath%..\IDE\mstest.exe"
"%toolpath%..\IDE\mstest.exe"  /testsettings:"..\Local.testsettings" /testContainer:"..\NuGetGallery.FunctionalTests\bin\NuGetGallery.FunctionalTests.dll" /test:"%Criteria%"

Echo Exit.

:End
exit /b 0
