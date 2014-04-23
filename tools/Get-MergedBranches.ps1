param([string]$ParentBranch = "master")

git branch --merged "origin/$ParentBranch" -r | 
        foreach { $_.Trim() } | 
        where { $_ -like "origin*" } |
        where { 
            ($_ -notlike "origin/pr*") -and 
            ($_ -notlike "origin/HEAD*") -and 
            (@("origin/master") -notcontains $_) 
        } | 
        foreach {
            $_.Substring("origin/".Length)
        }