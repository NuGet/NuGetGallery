<#
.SYNOPSIS
Copies app settings from one service to another

.DESCRIPTION

.PARAMETER From
The service to copy from

.PARAMETER To
The service to copy to
#>
function Copy-AppSettings {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact="high")]
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$From,
        [Parameter(Mandatory=$true, Position=1)][string]$To
    )
    if(!$CurrentEnvironment) {
        throw "This command requires an environment"
    }

    # Get the services
    $FromService = Get-NuGetService $From -ForceSingle
    $ToService = Get-NuGetService $To -ForceSingle
    if(!$FromService -or !$ToService) {
        return
    }

    Write-Host "Downloading configuration for the source service '$($FromService.ID)'"
    $FromDeployment = GetDeployment $FromService
    $FromSettings = Get-NuGetServiceConfiguration $FromDeployment
    
    Write-Host "Downloading configuration for the destination service '$($ToService.ID)'"
    $ToDeployment = GetDeployment $ToService
    $ToSettings = Get-NuGetServiceConfiguration $ToDeployment

    $Changes = @();
    $FromSettings.Keys | where { !$_.StartsWith("Microsoft.") } | foreach {
        if($ToSettings[$_] -ne $FromSettings[$_]) {
            $change = New-Object PSCustomObject;
            Add-Member -NotePropertyMembers @{
                "Key"=$_;
                "From"=$ToSettings[$_];
                "To"=$FromSettings[$_];
            } -InputObject $change
            $Changes += $change
        }
    }

    if($Changes.Length -eq 0) {
        Write-Host "No settings to update!"
    }
    else {
        $Changes | Format-Table Key,From,To
        if($PsCmdlet.ShouldProcess($ToService.ID, "Apply the above settings")) {
            $Changes | ForEach {
                $ToSettings[$_.Key] = $_.To
            }
            Set-NuGetServiceConfiguration $ToService $ToSettings
        }
    }

<#
    $FromSettings | foreach {
        if($ToSettings[$_] -ne $FromSettings[$_]) {
            Write-Verbose "Updating $_"
            $ToSettings[$_] = $FromSettings[$_]
        }
    }

    Write-Host "New Settings"
    $ToSettings
    #>
}