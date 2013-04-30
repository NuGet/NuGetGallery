function New-PublishSettingsFile {
    param(
        [string]$Name,
        [string]$HomeRealm)
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

    # Make a cert
    Write-Host "Generating Certificate..."
    $FileName = "$Name.cer"
    if(Test-Path $FileName) {
        throw "There is already a cert at $FileName. Delete it or move it before running this command"
    }
    makecert -sky exchange -r -n "CN=$Name" -pe -a sha1 -len 2048 -ss My $FileName

    # Get the Thumbprint and find the private key in the store
    $FileName = (Convert-Path $FileName)
    Write-Host "Certificate created. Public Key is at $FileName"
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $FileName

    $PublishSettingsFileName = [IO.Path]::ChangeExtension($FileName, "publishsettings");
    $CertData = [Convert]::ToBase64String($cert.Export("Pkcs12", [String]::Empty));
    $xmlTemplate = @"
    <PublishData>
      <PublishProfile
        PublishMethod="AzureServiceManagementAPI"
        Url="https://management.core.windows.net/"
        ManagementCertificate="$CertData">
        <Subscription
          Id="--- ID HERE ---"
          Name="--- NAME HERE ---" />
      </PublishProfile>
    </PublishData>
"@
    $xmlTemplate | Out-File -FilePath $PublishSettingsFileName -Encoding UTF8

    Start-Process "https://manage.windowsazure.com/$whr#Workspaces/AdminTasks/ListManagementCertificates"
    Write-Host "Now: Go upload $FileName to the Azure Portal. I've just launched your browser for you :)."
    Write-Host "I've written $PublishSettingsFileName but it's NOT READY YET!"
    Write-Host "Once you've uploaded the CER file for all the subscriptions you want to manage, open the publish settings file and find the Subscription element:"
    Write-Host
    Write-Host "<Subscription Id=`"--- ID HERE ---`" Name=`"--- NAME HERE ---`" />"
    Write-Host "Set the ID and Name as per the information in the portal. Feel free to add multiple copies with different ID/Name pairs. Just make sure you've uploaded the Cert to that subscription!"

}