@echo Off

REM Working directory one level up
cd ..

REM Configuration
set config=Release
set testCategory="P1Tests"
set solutionPath="NuGetGallery.FunctionalTests.sln"

REM Required Tools
set msbuild="%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild"
set xunit="..\packages\xunit.runner.console.2.0.0\tools\xunit.console.exe"
set nuget="nuget.exe"
set mstest="C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\mstest.exe"

REM Clean previous test results
if exist resultsfile.trx (
	del resultsfile.trx
)
if exist TestResults (
	rd TestResults /S /Q
)

REM Restore packages
if not exist nuget (
	PowerShell -NoProfile -ExecutionPolicy Bypass -File %cd%\Scripts\DownloadLatestNuGetExeRelease.ps1
)
call %nuget% restore "%solutionPath%" -NonInteractive
if not "%errorlevel%"=="0" goto failure

REM Build the solution
call %msbuild% "%solutionPath%" /p:Configuration="%config%" /p:Platform="Any CPU" /p:CodeAnalysis=true /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=diagnostic /nr:false
if not "%errorlevel%"=="0" goto failure

REM Run functional tests
set testDir="NuGetGallery.FunctionalTests\bin\%config%"
copy %nuget% %testDir%
call %xunit% "%testDir%\NuGetGallery.FunctionalTests.dll" -trait "Category=%testCategory%"

REM Run web UI and load tests
copy %nuget% .
call %mstest% /TestContainer:"NuGetGallery.WebUITests.P1\bin\%config%\NuGetGallery.WebUITests.P1.dll" /TestSettings:Local.testsettings /detail:stdout /resultsfile:resultsfile.trx
if not "%errorlevel%"=="0" goto failure

:success
exit 0

:failure
exit -1