param([switch]$Force)

$toClean = @(
    git branch --merged origin/master -r | 
    foreach { $_.Trim() } | 
    where { 
        ($_ -notmatch "origin/pr/\d+") -and 
        ($_ -match "origin/(?<b>[A-Za-z0-9-/]*).*") 
    } | 
    foreach { $matches.b } | 
    where { 
        @("HEAD","master","dev","dev-start","qa","staging") -notcontains $_ 
    })

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