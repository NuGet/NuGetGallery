@echo Off

REM Working directory one level up
cd ..

REM Configuration
set config=Release
set testCategory="P0Tests"
set solutionPath="NuGetGallery.FunctionalTests.sln"

REM Required Tools
set msbuild="%PROGRAMFILES(X86)%\MsBuild\14.0\Bin\msbuild"
set xunit="..\packages\xunit.runner.console.2.0.0\tools\xunit.console.exe"
set nuget="nuget.exe"
set mstest="C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\mstest.exe"
set vstest="C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"

REM Clean previous test results
if exist resultsfile.trx (
    del resultsfile.trx
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
call %xunit% "%testDir%\NuGetGallery.FunctionalTests.dll" -trait "Category=%testCategory%" -xml functionaltests.P0.xml

REM Run web UI tests
call %vstest% "NuGetGallery.WebUITests.P0\bin\%config%\NuGetGallery.WebUITests.P0.dll" /logger:trx
call %mstest% /TestContainer:"NuGetGallery.WebUITests.P0\bin\%config%\NuGetGallery.WebUITests.P0.dll" /TestSettings:Local.testsettings /detail:stdout /resultsfile:resultsfile.trx
if not "%errorlevel%"=="0" goto failure

REM Run Load tests
call %vstest% "NuGetGallery.LoadTests\bin\%config%\NuGetGallery.LoadTests.dll" /logger:trx /TestCaseFilter:"TestCategory=%testCategory%"
if not "%errorlevel%"=="0" goto failure

:success
exit 0

:failure
exit -1