@echo off

SET DIR=%~dp0%

%windir%\System32\WindowsPowerShell\v1.0\powershell.exe -NoExit -NoProfile -ExecutionPolicy unrestricted -Command "& '%DIR%ops\Enter-NuGetOps.ps1' -OwnConsole %*"