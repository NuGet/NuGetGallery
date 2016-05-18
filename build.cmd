@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)

set version=
if not "%PackageVersion%" == "" (
   set version=-Version %PackageVersion%
) else (
   set version=-Version 3.0.0-ci
)


REM Determine msbuild path
set msbuildtmp="%ProgramFiles%\MSBuild\14.0\bin\msbuild"
if exist %msbuildtmp% set msbuild=%msbuildtmp%
set msbuildtmp="%ProgramFiles(x86)%\MSBuild\14.0\bin\msbuild"
if exist %msbuildtmp% set msbuild=%msbuildtmp%
set VisualStudioVersion=14.0


REM Package restore
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockOpened name='Package restore']
)
echo.
echo Running package restore...
call :ExecuteCmd tools\nuget.exe restore NuGet.Services.Metadata.sln -OutputDirectory %cd%\packages -NonInteractive -ConfigFile .\NuGet.config
IF %ERRORLEVEL% NEQ 0 goto error
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockClosed name='Package restore']
)

REM Build
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockOpened name='Build solution']
)
echo.
echo Building solution...
call :ExecuteCmd %msbuild% "NuGet.Services.Metadata.sln" /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false
IF %ERRORLEVEL% NEQ 0 goto error
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockClosed name='Build solution']
)

REM Run tests
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockOpened name='Run tests']
)
echo.
echo Run tests...
call :ExecuteCmd tools\nuget.exe install xunit.runner.console -Version 2.1.0 -OutputDirectory packages
IF %ERRORLEVEL% NEQ 0 goto error
call :ExecuteCmd packages\xunit.runner.console.2.1.0\tools\xunit.console.exe tests\NgTests\bin\%config%\NgTests.dll
IF %ERRORLEVEL% NEQ 0 goto error
call :ExecuteCmd packages\xunit.runner.console.2.1.0\tools\xunit.console.exe tests\NuGet.IndexingTests\bin\%config%\NuGet.IndexingTests.dll
IF %ERRORLEVEL% NEQ 0 goto error
call :ExecuteCmd packages\xunit.runner.console.2.1.0\tools\xunit.console.exe tests\NuGet.Services.BasicSearchTests\bin\%config%\NuGet.Services.BasicSearchTests.dll
IF %ERRORLEVEL% NEQ 0 goto error
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockClosed name='Run tests']
)

REM Build cloud service
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockOpened name='Build cloud service']
)
echo.
echo Building cloud service...
call :ExecuteCmd %msbuild% "src\NuGet.Services.BasicSearch.Cloud\NuGet.Services.BasicSearch.Cloud.ccproj" /t:Publish /p:Configuration="%config%";TargetProfile=Cloud /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false
IF %ERRORLEVEL% NEQ 0 goto error
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockClosed name='Build cloud service']
)

REM Package
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockOpened name='Package artifacts']
)
echo.
echo Packaging artifacts...
mkdir artifacts
mkdir artifacts\packages
call :ExecuteCmd tools\nuget.exe pack "src\Catalog\NuGet.Services.Metadata.Catalog.csproj" -symbols -o artifacts\packages -p Configuration=%config% %version%
IF %ERRORLEVEL% NEQ 0 goto error
call :ExecuteCmd tools\nuget.exe pack "src\NuGet.Indexing\NuGet.Indexing.csproj" -symbols -o artifacts\packages -p Configuration=%config% %version%
IF %ERRORLEVEL% NEQ 0 goto error
call :ExecuteCmd tools\nuget.exe pack "src\NuGet.ApplicationInsights.Owin\NuGet.ApplicationInsights.Owin.csproj" -symbols -o artifacts\packages -p Configuration=%config% %version%
IF %ERRORLEVEL% NEQ 0 goto error

mkdir artifacts\octopus
call :ExecuteCmd tools\nuget.exe pack "src\Ng\Ng.csproj" -o artifacts\octopus -p Configuration=%config% %version% -NoPackageAnalysis
IF %ERRORLEVEL% NEQ 0 goto error
call :ExecuteCmd tools\nuget.exe pack "src\NuGet.Services.BasicSearch.Cloud\NuGet.Services.BasicSearch.Cloud.nuspec" -o artifacts\octopus -p Configuration=%config% %version% -NoPackageAnalysis
IF %ERRORLEVEL% NEQ 0 goto error
if not "%TEAMCITY_VERSION%" == "" (
	echo ##teamcity[blockClosed name='Package artifacts']
)

goto end

:: Execute command routine that will echo out when error
:ExecuteCmd
setlocal
set _CMD_=%*
call %_CMD_%
if "%ERRORLEVEL%" NEQ "0" echo Failed exitCode=%ERRORLEVEL%, command=%_CMD_%
exit /b %ERRORLEVEL%

:error
endlocal
echo An error has occurred during build.
call :exitSetErrorLevel
call :exitFromFunction 2>nul

:exitSetErrorLevel
exit /b 1

:exitFromFunction
()

:end
endlocal
echo Build finished successfully.
