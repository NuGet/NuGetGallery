param([switch]$Force, [string]$Subdomain="nuget", [string]$IISExpressPath, [string]$MakeCertPath, [string]$WebsitePath)

if(!(([Security.Principal.WindowsPrincipal]([System.Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator"))) {
    throw "This script must be run as an admin."
}

if(!$WebsitePath) {
    $WebsitePath = Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\src\NuGetGallery")

    if(!(Test-Path "$WebsitePath\NuGetGallery.csproj")) {
        throw "Could not find NuGetGallery project in $WebsitePath. Use -WebsitePath to specify the path to the folder containing NuGetGallery.csproj"
    }
}

# Find required tools
if(!$IISExpressPath) {
    $IISExpressPath = "$env:ProgramFiles\IIS Express"
    if(!(Test-Path $IISExpressPath)) {
        throw "Can't find IIS Express in $IISExpressPath. Use -IISExpressPath to specify the path to the folder containing IIS Express"
    }
}
$AppCmd = "$IISExpressPath\appcmd.exe"
if(!(Test-Path $AppCmd)) {
    throw "Can't find appcmd.exe in $IISExpressPath. Use -IISExpressPath to specify the path to the folder containing IIS Express"
}

if(!$MakeCertPath) {
    if($env:WindowsSdkDir) {
        $MakeCertPath = Join-Path $env:WindowsSdkDir "bin\x64\makecert.exe"
    }
    else {
        $pf32 = $env:ProgramFiles;
        if(Test-Path "env:\ProgramFiles(x86)") {
            $pf32 = cat "env:\ProgramFiles(x86)"
        }
        $MakeCertPath = Join-Path $pf32 "Windows Kits\8.1\bin\x64\makecert.exe"
        if(!(Test-Path $MakeCertPath)) {
            $MakeCertPath = Join-Path $pf32 "Windows Kits\8.0\bin\x64\makecert.exe"
        }
    }

    if(!(Test-Path $MakeCertPath)) {
        throw "Can't find makecert.exe in $MakeCertPath. Use -MakeCertPath to specify the path to makecert.exe. Default search paths include the Windows 8.0 and 8.1 SDK installation directories."
    }
}

# Enable access to the necessary URLs
netsh http add urlacl url=http://nuget.localtest.me:80/ user=Everyone
netsh http add urlacl url=https://nuget.localtest.me:443/ user=Everyone

$sites = @(&$AppCmd list site "NuGet Gallery ($Subdomain.localtest.me)")
if($sites.Length -gt 0) {
    if($Force) {
        &$AppCmd delete site "NuGet Gallery ($Subdomain.localtest.me)"
    } else {
       throw "You already have a site named `"NuGet Gallery ($Subdomain.localtest.me)`". Remove it manually or use -Force to have this command auto-remove it"
    }
}

&$AppCmd add site /name:"NuGet Gallery ($Subdomain.localtest.me)" /bindings:"http://$Subdomain.localtest.me:80,https://$Subdomain.localtest.me:443" /physicalPath:$WebsitePath
if($LASTEXITCODE -gt 0) { throw "IIS Site Update failed. Exit Code: $LASTEXITCODE" }

# Check for a cert
$cert = @(dir -l "Cert:\CurrentUser\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
if($cert.Length -eq 0) {
    $cert = @(dir -l "Cert:\LocalMachine\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
}

if($cert.Length -eq 0) {
    Write-Host "Generating a Self-Signed SSL Certificate for $Subdomain.localtest.me"
    # Generate one
    & $MakeCertPath -r -pe -n "CN=$Subdomain.localtest.me" -b `"$([DateTime]::Now.ToString("MM/dd/yyy"))`" -e `"$([DateTime]::Now.AddYears(10).ToString("MM/dd/yyy"))`" -eku 1.3.6.1.5.5.7.3.1 -ss root -sr localMachine -sky exchange -sp "Microsoft RSA SChannel Cryptographic Provider" -sy 12
    $cert = @(dir -l "Cert:\LocalMachine\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
}

if($cert.Length -eq 0) {
    throw "Failed to create an SSL Certificate"
}

Write-Host "Using SSL Certificate: $($cert.Thumbprint)"

# Set the Certificate
netsh http add sslcert hostnameport="$Subdomain.localtest.me:443" certhash="$($cert.Thumbprint)" certstorename=Root appid="{$([Guid]::NewGuid().ToString())}"

Write-Host -ForegroundColor Green "Complete! You should now be able to (Ctrl-)F5 the project!"
Write-Host -ForegroundColor Green "See errors about files already existing? That's OK! It just means you've run the script before."