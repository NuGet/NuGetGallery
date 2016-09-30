@echo Off

REM Working directory one level up
cd ..

REM Configuration
set config=Release
set solutionPath="BasicSearchTests.FunctionalTests.sln"

REM Required Tools
set msbuild="%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild"
set xunit=".\packages\xunit.runner.console.2.0.0\tools\xunit.console.exe"
set nuget="nuget.exe"

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
set testDir="BasicSearchTests.FunctionalTests.Core\bin\%config%"

echo "Running basic search functional core tests..."
call %xunit% "%testDir%\BasicSearchTests.FunctionalTests.Core.dll" -teamcity
if not "%errorlevel%"=="0" goto failure

:success
exit /b 0

:failure
exit /b -1