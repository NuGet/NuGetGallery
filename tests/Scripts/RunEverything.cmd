@echo Off

REM Working directory one level up
cd ..

set exitCode=0

REM Configuration
set config=Release
set solutionPath="NuGetGallery.FunctionalTests.sln"

REM Required Tools
set msbuild="%PROGRAMFILES(X86)%\MsBuild\14.0\Bin\msbuild"
set xunit="..\packages\xunit.runner.console.2.1.0\tools\xunit.console.exe"
set nuget="nuget.exe"
set mstest="C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\mstest.exe"
set vstest="C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"

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
set fluentTestDir="NuGetGallery.FunctionalTests.Fluent\bin\%config%"
copy %nuget% %testDir%
call %xunit% "%testDir%\NuGetGallery.FunctionalTests.dll" -xml functionaltests.everything.xml
if not "%errorlevel%"=="0" set exitCode=-1

copy %nuget% %fluentTestDir%
call %xunit% "%fluentTestDir%\NuGetGallery.FunctionalTests.Fluent.dll" -xml functionaltests.fluent.xml
if not "%errorlevel%"=="0" set exitCode=-1

REM Run web UI tests
call %mstest% /TestContainer:"NuGetGallery.WebUITests.P0\bin\%config%\NuGetGallery.WebUITests.P0.dll" /TestSettings:Local.testsettings /detail:stdout /resultsfile:resultsfile.web1.trx
if not "%errorlevel%"=="0" set exitCode=-1

REM Run web UI tests
call %mstest% /TestContainer:"NuGetGallery.WebUITests.P1\bin\%config%\NuGetGallery.WebUITests.P1.dll" /TestSettings:Local.testsettings /detail:stdout /resultsfile:resultsfile.web2.trx
if not "%errorlevel%"=="0" set exitCode=-1

REM Run web UI tests
call %mstest% /TestContainer:"NuGetGallery.WebUITests.P2\bin\%config%\NuGetGallery.WebUITests.P2.dll" /TestSettings:Local.testsettings /detail:stdout /resultsfile:resultsfile.web3.trx
if not "%errorlevel%"=="0" set exitCode=-1

REM Run Load tests
call %mstest% /TestContainer:"NuGetGallery.LoadTests\bin\%config%\NuGetGallery.LoadTests.dll" /TestSettings:Local.testsettings /detail:stdout /category:%testCategory% /resultsfile:resultsfile.load.trx
if not "%errorlevel%"=="0" set exitCode=-1

goto end

:failure
set exitCode=-1

:end
exit /B %exitCode%