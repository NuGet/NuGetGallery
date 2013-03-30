$save = $env:NUGET_OPS_ENVIRONMENTS
del env:\NUGET_*
if($save) {
    $env:NUGET_OPS_ENVIRONMENTS = $save
}
Remove-Module NuGetOps
Write-Host "Note: Only the NuGetOps module has been removed. The Azure module, etc. are still imported"
Set-Content function:\prompt $_OldPrompt