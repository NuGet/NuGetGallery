@echo Off

REM Working directory one level up
cd ..

REM Configuration
set config=Release
set solutionPath="NuGetServicesMetadata.FunctionalTests.sln"
set exitCode=0

REM Required Tools
set msbuild="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\msbuild"
set xunit=".\packages\xunit.runner.console.2.4.1\tools\net461\xunit.console.exe"
set nuget="nuget.exe"

REM Delete old test results
if exist functionaltests.*.xml (
    del functionaltests.*.xml
)

REM Restore packages
if not exist nuget (
    call PowerShell -NoProfile -ExecutionPolicy Bypass -File %cd%\Scripts\DownloadLatestNuGetExeRelease.ps1
)

echo "Restoring all solutions..."
call %nuget% restore "%solutionPath%" -NonInteractive
if not "%errorlevel%"=="0" goto failure

echo "Building solution..." %solutionPath%
REM Build the solution
call %msbuild% "%solutionPath%" /p:Configuration="%config%" /p:Platform="Any CPU" /p:CodeAnalysis=true /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=diagnostic /nr:false
if not "%errorlevel%"=="0" goto failure

REM Run functional tests

echo "Running basic search functional core tests..."
call %xunit% "BasicSearchTests.FunctionalTests.Core\bin\%config%\BasicSearchTests.FunctionalTests.Core.dll" -xml functionaltests.BasicSearchTests.xml
if not "%errorlevel%"=="0" set exitCode=-1

echo "Running Azure Search functional tests..."
call %xunit% "NuGet.Services.AzureSearch.FunctionalTests\bin\%config%\NuGet.Services.AzureSearch.FunctionalTests.dll" -xml functionaltests.AzureSearchTests.xml
if not "%errorlevel%"=="0" set exitCode=-1

exit /B %exitCode%

:failure
exit /b -1