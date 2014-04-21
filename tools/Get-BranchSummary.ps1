param([DateTime]$Before)
Write-Host "Calculating summary, this may take a few seconds..."
git branch -r | 
    foreach { $_.Trim() } | 
    where { ($_ -notlike "origin/pr/*") -and ($_ -notlike "origin/HEAD*") } |
    foreach { $_.Substring("origin/".Length) } |
    foreach { 
        $log = (git log "origin/$_" -n1 --oneline)
        $chunks = $log.Split(" ")
        $commit = $chunks[0]
        $comment = [String]::Join(" ", $chunks[1..($chunks.Length-1)])
        $obj = New-Object PSCustomObject
        Add-Member -InputObject $obj -NotePropertyMembers @{
            "Name" = $_;
            "Commit" = $commit;
            "Comment" = $comment;
            "Date" = [DateTime](@(git show -s --format=%ci $commit)[0]);
        }
        $obj
    } | 
    sort Date |
    where { (!$Before) -or ($_.Date -lt $Before) }
