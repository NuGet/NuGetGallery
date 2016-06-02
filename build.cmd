:CheckOS
IF EXIST "%PROGRAMFILES(X86)%" (GOTO 64BIT) ELSE (GOTO 32BIT)

:64BIT
"%programfiles(x86)%\MSBuild\14.0\Bin\msbuild.exe" build.msbuild /tv:14.0 /p:VisualStudioVersion=14.0 /p:ToolsVersion=14.0 %*
GOTO END

:32BIT
"%programfiles%\MSBuild\14.0\Bin\msbuild.exe" build.msbuild /tv:14.0 /p:VisualStudioVersion=14.0 /p:ToolsVersion=14.0 %*
GOTO END

:END