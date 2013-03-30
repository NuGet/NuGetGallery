<#
.SYNOPSIS
Exits the NuGet Operations Console and restores variables and prompts to their previous values
#>

$save = $env:NUGET_OPS_ENVIRONMENTS
del env:\NUGET_*
if($save) {
    $env:NUGET_OPS_ENVIRONMENTS = $save
}
Clear-Environment
Remove-Module NuGetOps
Write-Host "Note: Only the NuGetOps module has been removed. The Azure module, etc. are still imported"
Set-Content function:\prompt $_OldPrompt