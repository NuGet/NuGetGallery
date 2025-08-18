param([string]$SiteName = "NuGet Gallery", [string]$SitePhysicalPath, [string]$AppCmdPath)

function Get-SiteFQDN() { return "localhost" } # DevSkim: ignore DS162092
function Get-SiteHttpHost() { return "$(Get-SiteFQDN):80" }
function Get-SiteHttpsHost() { return "$(Get-SiteFQDN):443" }

function Get-SiteCertificate() {
    $cert = @(Get-ChildItem -l "Cert:\LocalMachine\Root" `
        | Where-Object { $_.Subject -eq "CN=$(Get-SiteFQDN)" }) `
        | Sort-Object -Property NotAfter -Descending `
        | Select-Object -First 1

    if (($null -ne $cert) -and ($cert.NotAfter -lt (Get-Date))) {
        Write-Host "Site certificate is expired. Recreating it. If you encounter issues, restart your computer." -ForegroundColor Yellow
        $cert = $null
    }
    
    if ($null -eq $cert) {
        Write-Host "Generating a self-signed SSL certificate for $(Get-SiteFQDN)"

        # Create a new self-signed certificate. New-SelfSignedCertificate can only create
        # certificates into the My certificate store.
        $newCert = New-SelfSignedCertificate -DnsName $(Get-SiteFQDN) -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(10)
        
        # Move the newly created self-signed certificate to the Root store.
        $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $rootStore.Add($newCert)
        $rootStore.Close()

        # Return the self-signed certificate from the Root store.
        $cert = @(Get-ChildItem -l "Cert:\LocalMachine\Root" `
            | Where-Object { $_.Thumbprint -eq $newCert.Thumbprint }) `
            | Select-Object -First 1
        if ($null -eq $cert) {
            throw "Failed to create an SSL certificate"
        }
    }

    return $cert
}

function Invoke-Netsh() {
    $argStr = $([String]::Join(" ", $args))
    $result = (netsh @args | Out-String).Trim()
    $parsed = [Regex]::Match($result, ".*Error: (\d+).*")

    if ($parsed.Success) {
        $err = $parsed.Groups[1].Value
        if (($err -ne "183") -and ($err -ne "2")) {
            throw $result
        }
    }
    elseif ($result -eq "The parameter is incorrect.") {
        throw "Parameters for netsh are incorrect:`r`n  $argStr"
    }
    else {
        Write-Host $result
    }
}

if (!(([Security.Principal.WindowsPrincipal]([System.Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator"))) {
    throw "This script must be run as an admin."
}

Write-Host "[BEGIN] Setting up IIS Express" -ForegroundColor Cyan

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (!$SitePhysicalPath) {
    $SitePhysicalPath = Join-Path $ScriptRoot "..\src\NuGetGallery"
}
if (!(Test-Path $SitePhysicalPath)) {
    throw "Could not find site at $SitePhysicalPath. Use -SitePhysicalPath argument to specify the path."
}
$SitePhysicalPath = Convert-Path $SitePhysicalPath

# Find IIS Express
if (!$AppCmdPath) {
    $IISXVersion = Get-ChildItem 'HKLM:\Software\Microsoft\IISExpress' | 
    ForEach-Object { New-Object System.Version ($_.PSChildName) } |
    Sort-Object -desc |
    Select-Object -first 1
    if (!$IISXVersion) {
        throw "Could not find IIS Express. Please install IIS Express before running this script, or use -AppCmdPath to specify the path to appcmd.exe for your IIS environment"
    }
    $IISRegKey = (Get-ItemProperty "HKLM:\Software\Microsoft\IISExpress\$IISXVersion")
    $IISExpressDir = $IISRegKey.InstallPath
    if (!(Test-Path $IISExpressDir)) {
        throw "Can't find IIS Express in $IISExpressDir. Please install IIS Express"
    }
    $AppCmdPath = "$IISExpressDir\appcmd.exe"
}

if (!(Test-Path $AppCmdPath)) {
    throw "Could not find appcmd.exe in $AppCmdPath!"
}

# Enable access to the necessary URLs
# S-1-1-0 is the unlocalized version for: user=Everyone 
Invoke-Netsh http delete urlacl "url=http://$(Get-SiteHttpHost)/" # DevSkim: ignore DS137138
Invoke-Netsh http delete urlacl "url=https://$(Get-SiteHttpsHost)/"
Invoke-Netsh http add urlacl "url=http://$(Get-SiteHttpHost)/" "sddl=D:(A;;GX;;;S-1-1-0)" # DevSkim: ignore DS137138
Invoke-Netsh http add urlacl "url=https://$(Get-SiteHttpsHost)/" "sddl=D:(A;;GX;;;S-1-1-0)"

$SiteFullName = "$SiteName ($(Get-SiteFQDN))"

$sites = @(& $AppCmdPath list site $SiteFullName)
if ($sites.Length -gt 0) {
    Write-Warning "Site '$SiteFullName' already exists. Deleting and recreating."
    & $AppCmdPath delete site "$SiteFullName"
}

& $AppCmdPath add site /name:"$SiteFullName" /bindings:"http://$(Get-SiteHttpHost),https://$(Get-SiteHttpsHost)" /physicalPath:$SitePhysicalPath # DevSkim: ignore DS137138

Write-Host "[DONE] Setting up IIS Express" -ForegroundColor Cyan

Write-Host "[BEGIN] Setting up SSL certificate" -ForegroundColor Cyan

$siteCert = Get-SiteCertificate
Write-Host "Using SSL Certificate: $($siteCert.Thumbprint)"
Invoke-Netsh http delete sslcert ipport="0.0.0.0:443"
Invoke-Netsh http delete sslcert hostnameport="$(Get-SiteHttpsHost)"
Invoke-Netsh http add sslcert hostnameport="$(Get-SiteHttpsHost)" certhash="$($siteCert.Thumbprint)" certstorename=Root appid="{$([Guid]::NewGuid().ToString())}"

Write-Host "[DONE] Setting up SSL certificate" -ForegroundColor Cyan

Write-Host "[BEGIN] Running Entity Framework migrations" -ForegroundColor Cyan

& "$ScriptRoot\Update-Databases.ps1" -MigrationTargets NuGetGallery, NuGetGallerySupportRequest -NuGetGallerySitePath $SitePhysicalPath

Write-Host "[DONE] Running Entity Framework migrations" -ForegroundColor Cyan