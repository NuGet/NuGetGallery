function Invoke-GalleryOperations() {
    param([switch]$WhatIf)

    if(!(Test-Path $GalOpsExe)) {
        Write-Warning "Gallery Ops Runner has not been built, build it and try your command again."
        return;
    }

    $tmpfile = $null
    if($CurrentDeployment) {
        # Write a temp file with config data
        $tmpfile = [IO.Path]::GetTempFileName()
        $CurrentDeployment.Backend.Configuration | Out-File -Encoding UTF8 -FilePath $tmpfile
    }

    # Fill Environment Variables
    $oldConfig = $null
    if(Test-Path "env:\NUGET_SERVICE_CONFIG") {
        $oldConfig = $env:NUGET_SERVICE_CONFIG
    }
    $env:NUGET_SERVICE_CONFIG = $tmpfile

    Write-Host $env:NUGET_SERVICE_CONFIG
    #& $GalOpsExe @args
    
    if($tmpfile -and (Test-Path $tmpfile)) {
        del $tmpfile
        if($oldConfig) {
            $env:NUGET_SERVICE_CONFIG = $oldConfig
        } else {
            del env:\NUGET_SERVICE_CONFIG
        }
    }
}
Set-Alias -Name galops -Value Invoke-GalleryOperations
Export-ModuleMember -Alias galops