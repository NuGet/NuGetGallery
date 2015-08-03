REM Install .NET Framework
set timehour=%time:~0,2%
set timestamp=%date:~-4,4%%date:~-10,2%%date:~-7,2%-%timehour: =0%%time:~3,2%
set startuptasklog=%PathToInstallLogs%\startuptasklog-%timestamp%.txt
set netfxinstallerlog = %PathToInstallLogs%\NetFXInstallerLog-%timestamp%
echo Logfile generated at: %startuptasklog% >> %startuptasklog%
echo Checking if .NET 4.5.2 is installed >> %startuptasklog%
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release | Find "0x5cbf5"
if %ERRORLEVEL%== 0 goto end
echo Installing .NET 4.5.2. Log: %netfxinstallerlog% >> %startuptasklog%
start /wait %~dp0NDP452-KB2901954-Web.exe /q /serialdownload /log %netfxinstallerlog%
:end
echo Completed: %date:~-4,4%%date:~-10,2%%date:~-7,2%-%timehour: =0%%time:~3,2% >> %startuptasklog%