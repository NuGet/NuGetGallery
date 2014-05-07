param([string]$Subdomain="nuget", [string]$SiteName = "NuGet Gallery", [string]$SitePhysicalPath, [string]$MakeCertPath, [string]$AppCmdPath)

if(!(([Security.Principal.WindowsPrincipal]([System.Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator"))) {
    throw "This script must be run as an admin."
}

if(!$SitePhysicalPath) {
    $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path;
    $SitePhysicalPath = Join-Path $ScriptRoot "..\src\NuGetGallery"
}
if(!(Test-Path $SitePhysicalPath)) {
    throw "Could not find site at $SitePhysicalPath. Use -SitePhysicalPath argument to specify the path."
}
$SitePhysicalPath = Convert-Path $SitePhysicalPath

# Find Windows SDK
if(!$MakeCertPath) {
    $SDKVersion = dir 'HKLM:\SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows' | 
        where { $_.PSChildName -match "v(?<ver>\d+\.\d+)" } | 
        foreach { New-Object System.Version $($matches["ver"]) } |
        sort -desc |
        select -first 1
    if(!$SDKVersion) {
        throw "Could not find Windows SDK. Please install the Windows SDK before running this script, or use -MakeCertPath to specify the path to makecert.exe"
    }
    $SDKRegKey = (Get-ItemProperty "HKLM:\SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows\v$SDKVersion")
    $WinSDKDir = $SDKRegKey.InstallationFolder
    $xArch = "x86"
    if($env:PROCESSOR_ARCHITECTURE -eq "AMD64") {
        $xArch = "x64"
    }
    $MakeCertPath = Join-Path $WinSDKDir "bin\$xArch\makecert.exe"
}

if(!(Test-Path $MakeCertPath)) {
    throw "Could not find makecert.exe in $MakeCertPath!"
}

# Find IIS Express
if(!$AppCmdPath) {
    $IISXVersion = dir 'HKLM:\Software\Microsoft\IISExpress' | 
        foreach { New-Object System.Version ($_.PSChildName) } |
        sort -desc |
        select -first 1
    if(!$IISXVersion) {
        throw "Could not find IIS Express. Please install IIS Express before running this script, or use -AppCmdPath to specify the path to appcmd.exe for your IIS environment"
    }
    $IISRegKey = (Get-ItemProperty "HKLM:\Software\Microsoft\IISExpress\$IISXVersion")
    $IISExpressDir = $IISRegKey.InstallPath
    if(!(Test-Path $IISExpressDir)) {
        throw "Can't find IIS Express in $IISExpressDir. Please install IIS Express"
    }
    $AppCmdPath = "$IISExpressDir\appcmd.exe"
}

if(!(Test-Path $AppCmdPath)) {
    throw "Could not find appcmd.exe in $AppCmdPath!"
}

function Invoke-Netsh() {
    $argStr = $([String]::Join(" ", $args))
    Write-Verbose "netsh $argStr"
    $result = netsh @args
    $parsed = [Regex]::Match($result, ".*Error: (\d+).*")
    if($parsed.Success) {
        $err = $parsed.Groups[1].Value
        if($err -ne "183") {
            throw $result
        }
    } else {
        Write-Host $result
    }
}

# Enable access to the necessary URLs
# S-1-1-0 is the unlocalized version for: user=Everyone 
Invoke-Netsh http add urlacl "url=http://$Subdomain.localtest.me:80/" "sddl=D:(A;;GX;;;S-1-1-0)"
Invoke-Netsh http add urlacl "url=https://$Subdomain.localtest.me:443/" "sddl=D:(A;;GX;;;S-1-1-0)"


$SiteFullName = "$SiteName ($Subdomain.localtest.me)"
$sites = @(&$AppCmdPath list site $SiteFullName)
if($sites.Length -gt 0) {
    Write-Warning "Site '$SiteFullName' already exists. Deleting and recreating."
    &$AppCmdPath delete site "$SiteFullName"
}

&$AppCmdPath add site /name:"$SiteFullName" /bindings:"http://$Subdomain.localtest.me:80,https://$Subdomain.localtest.me:443" /physicalPath:$SitePhysicalPath

# Check for a cert
$cert = @(dir -l "Cert:\CurrentUser\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
if($cert.Length -eq 0) {
    $cert = @(dir -l "Cert:\LocalMachine\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
}

if($cert.Length -eq 0) {
    Write-Host "Generating a Self-Signed SSL Certificate for $Subdomain.localtest.me"
    # Generate one
    & $MakeCertPath -r -pe -n "CN=$Subdomain.localtest.me" -b `"$([DateTime]::Now.ToString("MM\/dd\/yyy"))`" -e `"$([DateTime]::Now.AddYears(10).ToString("MM\/dd\/yyy"))`" -eku 1.3.6.1.5.5.7.3.1 -ss root -sr localMachine -sky exchange -sp "Microsoft RSA SChannel Cryptographic Provider" -sy 12
    $cert = @(dir -l "Cert:\LocalMachine\Root" | where {$_.Subject -eq "CN=$Subdomain.localtest.me"})
}

if($cert.Length -eq 0) {
    throw "Failed to create an SSL Certificate"
}

Write-Host "Using SSL Certificate: $($cert.Thumbprint)"

# Set the Certificate
Invoke-Netsh http add sslcert hostnameport="$Subdomain.localtest.me:443" certhash="$($cert.Thumbprint)" certstorename=Root appid="{$([Guid]::NewGuid().ToString())}"

Write-Host "Ready! All you have to do now is go to your website project properties and set 'http://$Subdomain.localtest.me' as your Project URL!"
