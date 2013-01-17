@echo off
powershell -ExecutionPolicy Unrestricted -NoProfile -NoLogo "& .\Startup.ps1" >> Startup.out.log

exit /b %errorlevel%