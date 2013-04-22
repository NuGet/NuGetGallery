param([switch]$Force, [string]$Subdomain="nuget")

$WebSite = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\Website")

# Enable access to the necessary URLs
netsh http add urlacl url=http://nuget.localtest.me:80/ user=Everyone
netsh http add urlacl url=https://nuget.localtest.me:443/ user=Everyone

$IISExpressDir = "$env:ProgramFiles\IIS Express"
if(!(Test-Path $IISExpressDir)) {
    throw "Can't find IIS Express in $IISExpressDir"
}
$AppCmd = "$IISExpressDir\appcmd.exe"

$sites = @(&$AppCmd list site "NuGet Gallery ($Subdomain.localtest.me)")
if($sites.Length -gt 0) {
    if($Force) {
        &$AppCmd delete site "NuGet Gallery ($Subdomain.localtest.me)"
    } else {
       throw "You already have a site named `"NuGet Gallery ($Subdomain.localtest.me)`". Remove it manually or use -Force to have this command auto-remove it"
    }
}

&$AppCmd add site /name:"NuGet Gallery ($Subdomain.localtest.me)" /bindings:"http://$Subdomain.localtest.me:80,https://$Subdomain.localtest.me:443" /physicalPath:$WebSite

# Check for a cert
$cert = @(dir -l "Cert:\CurrentUser\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
if($cert.Length -eq 0) {
    $cert = @(dir -l "Cert:\LocalMachine\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
}

if($cert.Length -eq 0) {
    Write-Host "Generating a Self-Signed SSL Certificate for $Subdomain.localtest.me"
    # Generate one
    & makecert -r -pe -n "CN=$Subdomain.localtest.me" -b `"$([DateTime]::Now.ToString("MM/dd/yyy"))`" -e `"$([DateTime]::Now.AddYears(10).ToString("MM/dd/yyy"))`" -eku 1.3.6.1.5.5.7.3.1 -ss root -sr localMachine -sky exchange -sp "Microsoft RSA SChannel Cryptographic Provider" -sy 12
    $cert = @(dir -l "Cert:\LocalMachine\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
}

if($cert.Length -eq 0) {
    throw "Failed to create an SSL Certificate"
}

Write-Host "Using SSL Certificate: $($cert.Thumbprint)"

# Set the Certificate
netsh http add sslcert hostnameport="$Subdomain.localtest.me:443" certhash="$($cert.Thumbprint)" certstorename=Root appid="{$([Guid]::NewGuid().ToString())}"

Write-Host "Ready! All you have to do now is go to your Website project properties and set 'http://$Subdomain.localtest.me' as your Project URL"
Write-Host "To use SSL, set the IISExpressSSLPort MSBuild property in your Website.csproj.user to 443"