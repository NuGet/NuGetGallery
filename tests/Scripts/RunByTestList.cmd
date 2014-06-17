@echo off

Echo Clear previulsly defined GalleryUrl
set GalleryURl=

SET List=%1
if "%List%"=="" (
  goto Error1
)

SET Param=%2
if "%Param%" NEQ "" (
ECHO Param is defined. Setting GalleryUrl to %Param%.
SET GalleryUrl=%Param%
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
Echo.
Echo. Build the NuGet Gallery solution...
call ..\..\build.cmd
Echo Done.
Echo.

Echo Build the NuGet Gallery test solution...
%WinDir%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe ..\NuGetGallery.FunctionalTests.sln
Echo Done.
Echo.

Echo Start running NuGet Gallery Functional tests by test list...
set
Echo The path to mstest.exe is "%toolpath%..\IDE\mstest.exe"
"%toolpath%..\IDE\mstest.exe"  /testlist:%List% /testmetadata:"..\NuGetGallery.FunctionalTests.vsmdi"
Echo Finished running NuGet Gallery Functional tests...
Echo Exit.
goto End

:Error1
Echo The variable testlist is not set correctly. It must be set for tests to run... 
goto End

:End
exit /b 0