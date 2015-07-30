@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)
set msbuild="%ProgramFiles(x86)%\MSBuild\14.0\bin\amd64\msbuild.exe"

REM Package restore
Powershell.exe -NoProfile -ExecutionPolicy ByPass -Command "& '%cd%\restoreNuGetExe.ps1'"
tools\nuget.exe restore NuGet.Jobs.sln -OutputDirectory %cd%\packages -NonInteractive

REM Build
%msbuild% NuGet.Jobs.sln /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false

REM Test
tools\nuget.exe install xunit.runner.console -Version 2.0.0 -OutputDirectory packages
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\Tests.Stats.CollectAzureCdnLogs\bin\%config%\Tests.Stats.CollectAzureCdnLogs.dll
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\Tests.Stats.ImportAzureCdnStatistics\bin\%config%\Tests.Stats.ImportAzureCdnStatistics.dll