<#
.SYNOPSIS
Lists the available configuration files for a service

.PARAMETER Type
The type of service to list configurations settings for
#>
function Get-ConfigurationSettings {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact="high")]
    param(
        [Parameter(Mandatory=$true, Position=1)][string]$Type
    )

    if(!$ConfigRoot) {
        return;
    }

    if(!$CurrentEnvironment) {
        throw "Requires a current environment"
    }

    $Config = Get-Configuration -Type $Type | Select-Object -First 1

    ParseConfigurationSettings $Config.File
}