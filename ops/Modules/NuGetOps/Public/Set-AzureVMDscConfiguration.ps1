function Set-AzureVMDscConfiguration {
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$Service,
        [Parameter(Mandatory=$true, Position=1)][string]$VMName,
        [Parameter(Mandatory=$true, Position=2)][string]$Role,
        [Parameter(Mandatory=$false, Position=3)][string]$Subscription,
        [Parameter(Mandatory=$false, Position=4)][string]$CertificateThumbprint)
    if(!(Test-Path $Role) -and $RoleConfigurationsDirectory) {
        $Role = Join-Path $RoleConfigurationsDirectory "$Role.dsc.ps1"
    }

    if(!(Test-Path $Role)) {
        throw "Could not find role: $Role"
    }

    $vm = GetAzureVMInfo $Service $VMName $Subscription $CertificateThumbprint

    & $Role -MachineName $vm.WinRMUri.Host
}