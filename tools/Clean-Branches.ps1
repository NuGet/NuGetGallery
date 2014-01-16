param([switch]$Force)

$toClean = & "$PSScriptRoot\Get-MergedBranches.ps1"

$pushargs = @($toClean | foreach { ":$_" })
if($Force) {
    git push origin @pushargs
} else {
    Write-Host "Pushing with -n (dry-run) without -Force parameter"
    git push origin -n @pushargs
}

$toClean | foreach {
    if($Force) {
        git branch -D $_
    } else {
        "Would delete local branch: $_"
    }
}