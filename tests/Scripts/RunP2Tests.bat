@echo Off

REM Working directory one level up
cd ..

REM Configuration
set config=Release
set testCategory="P2Tests"
set solutionPath="NuGetGallery.FunctionalTests.sln"
set exitCode=0

REM Required Tools
set msbuild="%PROGRAMFILES(X86)%\MsBuild\14.0\Bin\msbuild"
set xunit="..\packages\xunit.runner.console.2.1.0\tools\xunit.console.exe"
set nuget="nuget.exe"
set mstest="C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\mstest.exe"

REM Clean previous test results
if exist functionaltests.*.xml (
    del functionaltests.*.xml
)
if exist resultsfile.*.trx (
    del resultsfile.*.trx
)
if exist TestResults (
    rd TestResults /S /Q
)

REM Restore packages
if not exist nuget (
    call PowerShell -NoProfile -ExecutionPolicy Bypass -File %cd%\Scripts\DownloadLatestNuGetExeRelease.ps1
)
call %nuget% restore "%solutionPath%" -NonInteractive
if not "%errorlevel%"=="0" goto failure

REM Build the solution
call %msbuild% "%solutionPath%" /p:Configuration="%config%" /p:Platform="Any CPU" /p:CodeAnalysis=true /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=diagnostic /nr:false
if not "%errorlevel%"=="0" goto failure

REM Run functional tests
set testDir="NuGetGallery.FunctionalTests\bin\%config%"
copy %nuget% %testDir%
call %xunit% "%testDir%\NuGetGallery.FunctionalTests.dll" -trait "Category=%testCategory%" -xml functionaltests.P2.xml
if not "%errorlevel%"=="0" set exitCode=-1

REM Run web UI and load tests
call %mstest% /TestContainer:"NuGetGallery.WebUITests.P2\bin\%config%\NuGetGallery.WebUITests.P2.dll" /TestSettings:Local.testsettings /detail:stdout /resultsfile:resultsfile.web.trx
if not "%errorlevel%"=="0" set exitCode=-1

REM Run Load tests
call %mstest% /TestContainer:"NuGetGallery.LoadTests\bin\%config%\NuGetGallery.LoadTests.dll" /TestSettings:Local.testsettings /detail:stdout /category:%testCategory% /resultsfile:resultsfile.load.trx
if not "%errorlevel%"=="0" set exitCode=-1

exit /B %exitCode%

:failure
exit -1