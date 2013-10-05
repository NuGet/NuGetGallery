function Start-AzureVMDscConfiguration {
	param(
        [Parameter(Mandatory=$true, Position=0)][string]$Service,
        [Parameter(Mandatory=$true, Position=1)][string]$VMName,
        [Parameter(Mandatory=$true, Position=2)][string]$Role,
        [Parameter(Mandatory=$false)][string]$RoleConfigurationsDirectory = $Global:RoleConfigurationsDirectory,
        [Parameter(Mandatory=$false)][string]$Subscription,
        [Parameter(Mandatory=$false)][string]$CertificateThumbprint,
        [Parameter(Mandatory=$false)][switch]$Wait,
        [Parameter(Mandatory=$false)][switch]$WhatIf,
        [Parameter(Mandatory=$false)][switch]$Confirm,
        [Parameter(Mandatory=$false)][switch]$Force,
        [Parameter(Mandatory=$false)]$VMInfo)

    if(!$VMInfo) {
        $VMInfo = GetAzureVMInfo $Service $VMName $Subscription $CertificateThumbprint
    }
    $TargetHost = $VMInfo.WinRMUri.Host

    # Locate the DSC file
    $dsc = Join-Path $RoleConfigurationsDirectory "$Role.dsc.ps1"
    if(!(Test-Path $dsc)) {
        throw "Unable to locate Role DSC file $Role.dsc.ps1 in $RoleConfigurationsDirectory"
    }
    $dsc = Convert-Path $dsc

    # Locate the MOF file
    $mofPath = (Join-Path (Join-Path $RoleConfigurationsDirectory $Role) "$TargetHost.mof")
    if(!(Test-Path $mofPath)) {
        Write-Host "Could not file compiled DSC configuration in $RoleConfigurationsDirectory\$Role for $Service"
        Write-Host "Compiling now..."
        Publish-AzureVMDscConfiguration -Service $Service -Role $Role -RoleConfigurationsDirectory $RoleConfigurationsDirectory -VMInfo $VMInfo
    }
    if(!(Test-Path $mofPath)) {
        throw "Unable to compile DSC configuration..."
    }

    # Establish a connection
    $sess = Connect-AzureVM -Cim -Service $Service -VMName $VMName -Subscription $Subscription -CertificateThumbprint $CertificateThumbprint -VMInfo $VMInfo

    try {
        Start-DscConfiguration -Path (Split-Path -Parent $mofPath) -CimSession $sess -Wait:$Wait -WhatIf:$WhatIf -Force:$Force
    } finally {
        Remove-CimSession $sess
    }
}