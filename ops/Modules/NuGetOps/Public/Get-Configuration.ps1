<#
.SYNOPSIS
Lists the available configuration files for a service

.PARAMETER Type
The type of service to list configurations for
#>
function Get-Configuration {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact="high")]
    param(
        [Parameter(Mandatory=$false, Position=0)][string]$Type
    )

    if(!$ConfigRoot) {
        return;
    }

    dir "$ConfigRoot\*.cscfg" | ForEach-Object {
        $match = [Regex]::Match($_.Name, "(?<type>.*)\.(?<env>.*)\.cscfg")
        if($match.Success) {
            $cfg = New-Object PSCustomObject
            Add-Member -InputObject $cfg -NotePropertyMembers @{
                "Type" = $match.Groups["type"].Value;
                "Environment" = $match.Groups["env"].Value;
                "File" = $_.FullName
            }
            $cfg
        }
    } | Where-Object {
        (([String]::IsNullOrEmpty($Type)) -or ($Type -eq $_.Type)) -and
        (($CurrentEnvironment -eq $null) -or ($CurrentEnvironment.Name -eq $_.Environment))
    }
}