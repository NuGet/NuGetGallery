# Find IIS
$iisRoot = Join-Path $env:windir "system32\inetsrv"
if(Test-Path "HKLM:\Software\Microsoft\IISExpress") {
    $iisRoot = (Get-ItemProperty ((dir HKLM:\Software\Microsoft\IISExpress | sort -desc | select -first 1).PSPath)).InstallPath;
}

$appcmd = Join-Path $iisRoot "appcmd.exe"
if(!(Test-Path $appcmd)) {
    throw "Could not find AppCmd!"
}

Import-Module ServerManager
[Reflection.Assembly]::LoadWithPartialName("Microsoft.WindowsAzure.ServiceRuntime");

# Wait for the site to be provisioned
while ((Get-Website).Count -eq 0) {
    Write-Host "Waiting for website to be provisioned..."
    Start-Sleep 10
}

# Configure secondary SSL bindings
$setting = [Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment]::GetConfigurationSettingValue("Startup.AdditionalSSL");
$additionalSSL = $setting.Split(","); # e.g. foo.bar:443:thumbprint,bar.baz:443:FEDCBA

# Register additional SSL bindings - note that the certificates must be added to the VM (ServiceDefinition.csdef)
$sites = [xml](&$appcmd list sites /xml)
$defaultSite = $null
if ($sites.appcmd.SITE -is [system.array]) {
	$defaultSite = $sites.appcmd.SITE[0].Attributes[0].Value.ToString() 
} else {
	$defaultSite = $sites.appcmd.SITE.Attributes[0].Value.ToString()
}
$ethernetIp = Get-NetIPAddress | Where-Object { $_.AddressFamily -eq "IPv4" -and $_.PrefixOrigin -eq "Dhcp" }
$ethernetIp = $ethernetIp.IPv4Address

$additionalSSL | where { ![String]::IsNullOrEmpty($_) } | foreach {
    $parts = $_.Split(":")`

	$hostname = $parts[0]
	$port = $parts[1]
	$thumbprint = $parts[2]
	
	echo "Adding binding to site $defaultSite for URL https://$hostname`:$port with SNI certificate $thumbprint"
	&$appcmd set site /site.name:"$defaultSite" /+"bindings.[protocol='https',bindingInformation='$ethernetIp`:$port`:$hostname',sslFlags='1']" /commit:apphost
	netsh http add sslcert hostnameport=$hostname`:$port certhash=$thumbprint appid='{4dc3e181-e14b-4a21-b022-59fc669b0914}' certstorename=MY

	&$appcmd set site /site.name:"$defaultSite" /-"bindings.[protocol='https',bindingInformation='$ethernetIp`:443:']" /commit:apphost
    netsh http delete sslcert ipport=$ethernetIp`:443
}
