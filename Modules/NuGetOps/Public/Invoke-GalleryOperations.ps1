function Invoke-GalleryOperations() {
    if(!(Test-Path $OpsRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe)) {
        Write-Warning "Gallery Ops Runner not built. Building..."
        msbuild $OpsRoot\NuGetGalleryOps.sln
        Write-Warning "Gallery Ops Runner has been built, try your command again."
        return;
    }

    if($args.Length -eq 0) {
        & $OpsRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe
        return;
    }

    $tmpfile;
    if($CurrentDeployment) {
        # Write a temp file with config data
        $tmpfile = [IO.Path]::GetTempFileName()
        $CurrentDeployment["Worker"].Configuration | Out-File -Encoding UTF8 -FilePath $tmpfile
    }
    try {
        if($tmpfile) {
            & $OpsRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe @args -ConfigFile $tmpfile -EnvironmentName $CurrentEnvironment.Name
        } else {
            & $OpsRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe @args
        }
    } finally {
        if($tmpfile -and (Test-Path $tmpfile)) {
            del $tmpfile
        }
    }
}
Set-Alias -Name galops -Value Invoke-GalleryOperations
Export-ModuleMember -Alias galops