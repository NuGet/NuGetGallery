@echo off
powershell -ExecutionPolicy Unrestricted -NoProfile -NoLogo "& .\Startup.ps1" >> Startup.out.log

REM Always exit successfully, otherwise Azure might become very sad. And no-one wants a sad Azure.
exit /b 0