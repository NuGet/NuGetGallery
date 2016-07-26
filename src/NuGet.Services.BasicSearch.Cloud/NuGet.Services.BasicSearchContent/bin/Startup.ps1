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
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/json',enabled='True']" /commit:apphost
&$appcmd set config -section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/json; charset=utf-8',enabled='True']" /commit:apphost

# Customize Logging
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.enabled:"True" /commit:apphost
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.logFormat:"W3C" /commit:apphost
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.period:"Hourly" /commit:apphost
&$appcmd set config -section:system.applicationHost/sites /siteDefaults.logFile.logExtFileFlags:"Date,Time,TimeTaken,BytesRecv,BytesSent,ComputerName,HttpStatus,HttpSubStatus,Win32Status,ProtocolVersion,ServerIP,ServerPort,Method,Host,UriStem,UriQuery,UserAgent"

# Increase the number of available IIS threads for high performance applications
# Uses the recommended values from http://msdn.microsoft.com/en-us/library/ms998549.aspx#scalenetchapt06_topic8
# Assumes running on two cores (medium instance on Azure)
&$appcmd set config /commit:MACHINE -section:processModel -maxWorkerThreads:100
&$appcmd set config /commit:MACHINE -section:processModel -minWorkerThreads:50
&$appcmd set config /commit:MACHINE -section:processModel -minIoThreads:50
&$appcmd set config /commit:MACHINE -section:processModel -maxIoThreads:100
 
# Adjust the maximum number of connections per core for all IP addresses
&$appcmd set config /commit:MACHINE -section:connectionManagement /+["address='*',maxconnection='300'"]

# Disable app pool timeout
&$appcmd set config -section:applicationPools -applicationPoolDefaults.processModel.idleTimeout:00:00:00
&$appcmd set config -section:applicationPools -applicationPoolDefaults.recycling.periodicRestart.time:24:00:00

# Increase request queue size
&$appcmd set config -section:applicationPools -applicationPoolDefaults.queueLength:2000

# Install and enable Application Initialization into IIS
pkgmgr.exe /iu:IIS-ApplicationInit

&$appcmd set config -section:applicationPools /applicationPoolDefaults.startMode:AlwaysRunning
&$appcmd list sites "/name:$=NuGet*" /xml | &$appcmd set site /in /serverAutoStart:true
&$appcmd list sites "/name:$=NuGet*" /xml | &$appcmd set site /in /applicationDefaults.preloadEnabled:true

# Install Microsoft internal corporate root required for KeyVault access to LocalMachine\AuthRoot (it's not supported by azure, hence we install to cert to CA store, and then need to move it) 
Move-Item -Path Cert:\LocalMachine\CA\D17697CC206ED26E1A51F5BB96E9356D6D610B74 -Destination Cert:\LocalMachine\Root

