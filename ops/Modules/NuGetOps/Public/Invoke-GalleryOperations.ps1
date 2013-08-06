function Invoke-GalleryOperations() {
    if(!(Test-Path $GalOpsExe)) {
        Write-Warning "Gallery Ops Runner has not been built, build it and try your command again."
        return;
    }

    if($args.Length -eq 0) {
        & $GalOpsExe
        return;
    }

    $tmpfile;
    if($CurrentDeployment) {
        # Write a temp file with config data
        $tmpfile = [IO.Path]::GetTempFileName()
        $CurrentDeployment.Backend.Configuration | Out-File -Encoding UTF8 -FilePath $tmpfile
    }
    try {
        if($tmpfile) {
            & $GalOpsExe @args -ConfigFile $tmpfile -EnvironmentName $CurrentEnvironment.Name
        } else {
            & $GalOpsExe @args
        }
    } finally {
        if($tmpfile -and (Test-Path $tmpfile)) {
            del $tmpfile
        }
    }
}
Set-Alias -Name galops -Value Invoke-GalleryOperations
Export-ModuleMember -Alias galops