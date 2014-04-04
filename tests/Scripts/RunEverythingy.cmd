@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

Echo Set environment variable RunFuntionalTests to True to enable running the functional tests
set RunFuntionalTests=true

Echo Clear previulsly defined GalleryUrl
set GalleryURl=

SET Param=%1
if "%Param%" NEQ "" (
ECHO Param is defined. Setting GalleryUrl to %Param%.
SET GalleryUrl=%Param%
) 

REM If GalleryUrl is still not defined, the default is to use int.nugettest.org
if "%GalleryUrl%"=="" (
ECHO Setting GalleryUrl to the default - int.nugettest.org
SET GalleryUrl=https://int.nugettest.org
)
ECHO The NuGet gallery tests are running against %GalleryUrl%

If Exist ""%VS120COMNTOOLS%"\..\IDE\mstest.exe" (
   set toolpath=%VS120COMNTOOLS%
   goto Run
)

If Exist ""%VS110COMNTOOLS%"..\IDE\mstest.exe" (
   set toolpath=%VS110COMNTOOLS%
   goto Run
)

:Error
Echo The variable toolpath is not set correctly. check your visual studio install. Exiting without running tests... 
goto End

:Run
Echo.
Echo Start running NuGet Gallery Functional tests...
Echo The path to mstest.exe is "%toolpath%..\IDE\mstest.exe"
"%toolpath%..\IDE\mstest.exe"  /testsettings:"..\Local.testsettings" /testmetadata:"..\NuGetGallery.FunctionalTests.vsmdi"
Echo Finished running NuGet Gallery Functional tests...
Echo Exit.

:End
endlocal
exit /b 0