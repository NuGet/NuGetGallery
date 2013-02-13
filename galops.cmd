@echo off

if exist %~dp0\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe goto run

echo Gallery Ops Runner not built. Building...
msbuild %~dp0\NuGetGalleryOps.sln
echo Gallery Ops Runner has been built, try your command again.
goto end

:run
%~dp0\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe %*

:end