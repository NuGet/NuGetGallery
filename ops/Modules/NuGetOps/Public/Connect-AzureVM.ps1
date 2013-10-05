function Connect-AzureVM {
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$Service,
        [Parameter(Mandatory=$true, Position=1)][string]$VMName,
        [Parameter(Mandatory=$false)][string]$Subscription,
        [Parameter(Mandatory=$false)][string]$CertificateThumbprint,
        [Parameter(Mandatory=$false, ParameterSetName="PSRemoting")][switch]$DoNotEnter,
        [Parameter(Mandatory=$true, ParameterSetName="CIM")][switch]$Cim,
        [Parameter(Mandatory=$false)]$VMInfo)

    if(!$VMInfo) {
        $VMInfo = GetAzureVMInfo $Service $VMName $Subscription $CertificateThumbprint
    }
    
    
    $cred = Get-Credential -Message "Enter Admin Credentials..."
    if(!$cred) {
        throw "User cancelled credential dialog"
    }

    if($Cim) {
        Write-Host "Connecting to CIM Service..."
        $options = New-CimSessionOption -UseSsl
        New-CimSession -ComputerName $VMInfo.WinRMUri.Host -Port $VMInfo.WinRMUri.Port -Credential $cred -SessionOption $options
    }
    else {
        Write-Host "Connecting to PowerShell Remoting..."
        $session = New-PSSession -ConnectionUri $VMInfo.WinRMUri -Credential $cred
        if(!$DoNotEnter) {
            Enter-PSSession $session
        } else {
            $session
        }
    }
}