# Find IIS
$iisRoot = Join-Path $env:windir "system32\inetsrv"
if(Test-Path "HKLM:\Software\Microsoft\IISExpress") {
    $iisRoot = (Get-ItemProperty ((dir HKLM:\Software\Microsoft\IISExpress | sort -desc | select -first 1).PSPath)).InstallPath;
}

$appcmd = Join-Path $iisRoot "appcmd.exe"
if(!(Test-Path $appcmd)) {
    throw "Could not find AppCmd!"
}

# Enable Dynamic Compression of OData feed
&$appcmd set config /section:urlCompression /doDynamicCompression:True /commit:apphost
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/json; charset=utf-8',enabled='True']" /commit:apphost
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/xml; charset=utf-8',enabled='True']" /commit:apphost
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/xml',enabled='True']" /commit:apphost
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/atom%u002bxml; charset=utf-8',enabled='True']" /commit:apphost
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/atom%u002bxml',enabled='True']" /commit:apphost

# Customize Logging
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.enabled:"True" /commit:apphost
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.logFormat:"W3C" /commit:apphost
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.period:"Hourly" /commit:apphost
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.logExtFileFlags:"Date,Time,TimeTaken,BytesRecv,BytesSent,ComputerName,HttpStatus,HttpSubStatus,Win32Status,ProtocolVersion,ServerIP,ServerPort,Method,Host,UriStem,UriQuery,UserAgent"