@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)

REM Package restore
Powershell.exe -NoProfile -ExecutionPolicy ByPass -Command "& '%cd%\restoreNuGetExe.ps1'"
tools\nuget.exe restore NuGet.Jobs.sln -OutputDirectory %cd%\packages -NonInteractive

REM Build
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild NuGet.Jobs.sln /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false

REM Test
tools\nuget.exe install xunit.runner.console -Version 2.0.0 -OutputDirectory packages
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\Tests.Stats.CollectAzureCdnLogs\bin\%config%\Tests.Stats.CollectAzureCdnLogs.dll
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\Tests.Stats.ParseAzureCdnLogs\bin\%config%\Tests.Stats.ParseAzureCdnLogs.dll