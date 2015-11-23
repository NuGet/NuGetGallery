@echo Off
set config=%1
if "%config%" == "" (
   set config=Release
)

REM Package restore
tools\nuget.exe restore NuGet.Services.Metadata.sln -OutputDirectory %cd%\packages -NonInteractive

REM Build
"%programfiles(x86)%\MSBuild\14.0\Bin\msbuild" NuGet.Services.Metadata.sln /p:Configuration="%config%" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false

REM Test
tools\nuget.exe install xunit.runner.console -Version 2.0.0 -OutputDirectory packages
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe NgTests\bin\%config%\NgTests.dll
packages\xunit.runner.console.2.0.0\tools\xunit.console.exe tests\NuGetFeedTests\bin\%config%\NuGetFeedTests.dll