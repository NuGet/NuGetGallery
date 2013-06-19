<#
.SYNOPSIS
Creates an Azure Publish Settings File

.DESCRIPTION
This command has three different behaviors.

If given the "-CertificateThumbprint" argument, it expects to find a certificate in either the cert:\CurrentUser\My or cert:\LocalMachine\My stores
with an exportable Private Key. It then uses that to generate a publish settings file.

If given the "-PfxFile" argument, it imports the cert into the cert:\CurrentUser\My store and uses it to generate a publish settings file

If given neither argument, it generates a new cert in the cert:\CurrentUser\My store and uses it to generate a publish settings file

.PARAMETER Name
The name of the publish settings file to create (using the default is recommended)

.PARAMETER HomeRealm
The Azure Home Realm to use (i.e. "microsoft.com" for Microsoft Corporate Azure resources, etc.). Leave blank if you don't know what it is.

.PARAMETER NoBrowser
Set this switch to stop the browser from launching

.PARAMETER Import
Set this switch to import the publish settings immediately, but only if there were subscriptions found.

.PARAMETER CertificateThumbprint
The thumbprint (as a hex string) of the certificate to use as the management certificate

.PARAMETER PfxFile
The path to a Pfx file to use as the management certificate
#>

function New-PublishSettingsFile {
    [CmdletBinding(DefaultParameterSetName="Generate")]
    param(
        [Parameter(Mandatory=$false)][string]$Name,
        [Parameter(Mandatory=$false)][string]$HomeRealm,
        [Parameter(Mandatory=$false)][switch]$NoBrowser,
        [Parameter(Mandatory=$false)][switch]$Import)
    $MsftDomainNames = @("REDMOND","FAREAST","NORTHAMERICA","NTDEV")
    if(!$Name) {
        $Name = "Azure-$([Environment]::UserName)-on-$([Environment]::MachineName)-at-$([DateTime]::UtcNow.ToString("yyyy-MM-dd"))-utc"
    }
    $whr = ""
    if($HomeRealm) {
        $whr = "?whr=$HomeRealm"
    } elseif($MsftDomainNames -contains [Environment]::UserDomainName) {
        $whr = "?whr=microsoft.com"
    }

    $PublishSettingsFileName = "$Name.publishsettings";

    # Make a cert
    Write-Host "Generating Certificate..."
    $FileName = Join-Path (Convert-Path .) "$Name.cer"
    if(Test-Path $FileName) {
        throw "There is already a cert at $FileName. Delete it or move it before running this command"
    }
    $expiry = [DateTime]::Now.AddYears(1).ToString("MM/dd/yyyy");
    makecert -sky exchange -r -n "CN=$Name" -pe -a sha1 -len 2048 -ss My -sr CurrentUser $FileName -e $expiry

    # Get the Thumbprint and find the private key in the store
    $PublicOnlyCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $FileName
    $CertificateThumbprint = $PublicOnlyCert.Thumbprint
    $cert = Get-Item "cert:\CurrentUser\My\$CertificateThumbprint"
    if(!$cert.HasPrivateKey -or $cert.PrivateKey -eq $null) {
        throw "Don't have a private key for the cert we just made??"
    }
    $cert.FriendlyName = "Windows Azure Management Service Authentication Certificate"

    # Export to a PFX
    $PfxFile = Join-Path (Convert-Path .) "$Name.pfx"
    $bytes = $cert.Export("Pkcs12", [String]::Empty)
    [IO.File]::WriteAllBytes($PfxFile, $bytes)
    $CertData = [Convert]::ToBase64String($bytes);

    Write-Host "Certificate created. Public Key is at $FileName"
    Write-Host "Private Key is at: $PfxFile"
    Write-Host "Thumbprint: $CertificateThumbprint"
    
    $subsXml = @"
        <Subscription
          Id="--- ID HERE ---"
          Name="--- NAME HERE ---" />
"@
    if($Subscriptions) {
        Write-Host 'Using Subscriptions from current operations environment'
        $subsXml = $Subscriptions.Keys | foreach {
            @"
        <Subscription
          Id="$($Subscriptions[$_].Id)"
          Name="$($Subscriptions[$_].Name)" />

"@
        }
    } else {
        $Import = $false;
    }

    $xmlTemplate = @"
    <PublishData>
      <PublishProfile
        PublishMethod="AzureServiceManagementAPI"
        Url="https://management.core.windows.net/"
        ManagementCertificate="$CertData">
$subsXml
      </PublishProfile>
    </PublishData>
"@
    $xmlTemplate | Out-File -FilePath $PublishSettingsFileName -Encoding UTF8

    if(!$NoBrowser) {
        Start-Process "https://manage.windowsazure.com/$whr#Workspaces/AdminTasks/ListManagementCertificates"
        Write-Host "Now: Go upload $FileName to the Azure Portal for each subscription you want to manage. I've just launched your browser for you :)."
    }
    if($Subscriptions) {
        Write-Host "The following subscriptions have been defined in the Publish Settings File:"
        $Subscriptions.Keys | foreach { "* $_" }
    } else {
        Write-Host "I didn't find any subscription to define, so you'll have to update the Publish Settings file manually."
    }
}