function Connect-AzureVM {
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$Service,
        [Parameter(Mandatory=$true, Position=1)][string]$VMName,
        [Parameter(Mandatory=$false, Position=2)][string]$Subscription,
        [Parameter(Mandatory=$false, Position=3)][string]$CertificateThumbprint)
    $vm = GetAzureVMInfo $Service $VMName $Subscription $CertificateThumbprint
    
    Write-Host "Connecting to PowerShell..."
    $cred = Get-Credential -Message "Enter Admin Credentials..."
    $session = New-PSSession -ConnectionUri $vm.WinRMUri -Credential $cred

    Enter-PSSession $session
}