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

# Configure IP Restrictions

#  Install the feature
Import-Module ServerManager
Add-WindowsFeature Web-IP-Security

#  Clear them
do {
    $str = &$appcmd set config -section:system.webServer/security/ipSecurity /-"[@start]" /commit:apphost
    $str
} while(!$str.Contains("ERROR"))

#  Read the new list
[Reflection.Assembly]::LoadWithPartialName("Microsoft.WindowsAzure.ServiceRuntime");
$setting = [Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment]::GetConfigurationSettingValue("Startup.BlockedIPs");
$ips = $setting.Split(",");

#  Save the new lists
$ips | where { ![String]::IsNullOrEmpty($_) } | foreach { 
    $parts = $_.Split(":")
    $ip = $parts[0]
    if($parts.Length -gt 1) {
        $subnet = $parts[1]
        &$appcmd set config -section:system.webServer/security/ipSecurity /+"[ipAddress='$ip',subnetMask='$subnet',allowed='False']" /commit:apphost
    }
    else {
        &$appcmd set config -section:system.webServer/security/ipSecurity /+"[ipAddress='$ip',allowed='False']" /commit:apphost
    }
}

# Configure secondary SSL bindings
$setting = [Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment]::GetConfigurationSettingValue("Startup.AdditionalSSL");
$additionalSSL = $setting.Split(","); # e.g. foo.bar:443:thumbprint,bar.baz:443:FEDCBA

# Register additional SSL bindings
$sites = [xml](&$appcmd list sites /xml)
$defaultSite = $sites.appcmd.SITE[0].Attributes[0].Value.ToString()
$additionalSSL | where { ![String]::IsNullOrEmpty($_) } | foreach {
    $parts = $_.Split(":")`

	$hostname = $parts[0]
	$port = $parts[1]
	$thumbprint = $parts[2]
	
	echo Adding binding to site $defaultSite for URL https://$hostname`:$port with SNI certificate $thumbprint
	&$appcmd set site /site.name:"$defaultSite" /+"bindings.[protocol='https',bindingInformation='*:$port`:$hostname',sslFlags='1']" /commit:apphost
	netsh http add sslcert hostnameport=$hostname`:$port certhash=$thumbprint appid='{4dc3e181-e14b-4a21-b022-59fc669b0914}' certstorename=MY
}
