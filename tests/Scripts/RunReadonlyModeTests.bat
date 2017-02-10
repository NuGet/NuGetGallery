@echo Off

REM Working directory one level up
cd ..

REM Configuration
set config=Release
set testCategory="ReadonlyModeTests"
set solutionPath="NuGetGallery.FunctionalTests.sln"

REM Required Tools
set msbuild="%PROGRAMFILES(X86)%\MsBuild\14.0\Bin\msbuild"
set xunit="..\packages\xunit.runner.console.2.1.0\tools\xunit.console.exe"
set nuget="nuget.exe"
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
call %xunit% "%testDir%\NuGetGallery.FunctionalTests.dll" -trait "Category=%testCategory%" -xml functionaltests.readonly.xml

REM Run web UI and load tests
call %vstest% /TestContainer:"NuGetGallery.WebUITests.ReadOnlyMode\bin\%config%\NuGetGallery.WebUITests.ReadOnlyMode.dll" /logger:trx
if not "%errorlevel%"=="0" goto failure

REM Run Load tests
call %vstest% /TestContainer:"NuGetGallery.LoadTests\bin\%config%\NuGetGallery.LoadTests.dll" /logger:trx /TestCaseFilter:"TestCategory=%testCategory%"
if not "%errorlevel%"=="0" goto failure

:success
exit 0

:failure
exit -1