@echo off

REM Enable Dynamic HTTP Compression for application/* mime types.
%windir%\system32\inetsrv\appcmd.exe set config /section:urlCompression /doDynamicCompression:True /commit:apphost
%windir%\system32\inetsrv\appcmd.exe set config /section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/*',enabled='True']" /commit:apphost
