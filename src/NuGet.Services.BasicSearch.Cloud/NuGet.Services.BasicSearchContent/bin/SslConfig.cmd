powershell -NoProfile -ExecutionPolicy Unrestricted -File "%~dp0SslConfig.ps1" >> startup.log 2>> startup.err
exit /b 0