function GetAzureVMInfo {
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$Service,
        [Parameter(Mandatory=$true, Position=1)][string]$VMName,
        [Parameter(Mandatory=$false, Position=2)][string]$Subscription,
        [Parameter(Mandatory=$false, Position=3)][string]$CertificateThumbprint)

    if($Subscription) {
        $OldSubscription = Get-AzureSubscription | Where-Object { $_.IsDefault };
        Select-AzureSubscription $Subscription
    }

    if($CertificateThumbprint) {
        $cert = (dir "cert:\CurrentUser\Root\$CertificateThumbprint")
    }
    else {
        $certs = @(dir cert:\CurrentUser\Root | where { $_.Subject -eq "CN=$VMName" })
        if($certs.Length -eq 0) {
            $certInfo = Get-AzureCertificate $Service
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 (,([Convert]::FromBase64String($certInfo.Data)))
            $store = (Get-Item cert:\CurrentUser\Root)
            $store.Open("ReadWrite")
            $store.Add($cert)
            $store.Close()
        } elseif($certs.Length -eq 1) {
            $cert = $certs[0]
        } else {
            throw "Multiple certs for $VMName found. Use the -CertificateThumbprint to specify one"
        }
    }

    Write-Host "Locating WinRM Endpoint for $VMName in $Service ..."
    $vm = Get-AzureVM $Service $VMName
    $port = ($vm.VM.ConfigurationSets.InputEndpoints | where { $_.Name -eq "PowerShell" }).Port
    $uriBuilder = (New-Object UriBuilder (New-Object Uri $vm.DNSName))
    $uriBuilder.Port = $port;
    $uriBuilder.Scheme = "https";

    # Restore the old subscription, if any
    if($OldSubscription) {
        Select-AzureSubscription $OldSubscription
    }

    $ret = New-Object PSCustomObject
    Add-Member -InputObject $ret -NotePropertyMembers @{
        "VM"=$vm;
        "WinRMUri"=$uriBuilder.Uri;
        "Certificate"=$cert;
    }
    $ret
}