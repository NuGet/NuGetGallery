function Publish-AzureVMDscConfiguration {
    param(
        [Parameter(Mandatory=$true, Position=1)][string]$Service,
        [Parameter(Mandatory=$true, Position=2)][string]$Role,
        [Parameter(Mandatory=$false)][string]$RoleConfigurationsDirectory = $Global:RoleConfigurationsDirectory,
        [Parameter(Mandatory=$false)]$VMInfo)
    
    if(!$VMInfo) {
        $VMInfo = GetAzureVMInfo $Service $VMName $Subscription $CertificateThumbprint
    }

    if(!$RoleConfigurationsDirectory) {
        $RoleConfigurationsDirectory = Get-Location
    }

    $File = Join-Path $RoleConfigurationsDirectory "$Role.dsc.ps1"
    
    if(!(Test-Path $File)) {
        throw "Could not find role $Role in $RoleConfigurationsDirectory."
    }

    pushd $RoleConfigurationsDirectory
    . $File
    & $Role -MachineName $VMInfo.WinRMUri.Host | Out-Null
    popd

    Write-Host "Configuration Compiled and saved in $RoleConfigurationsDirectory\$Role"
}