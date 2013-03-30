$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if(!(Test-Path $ScriptRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe)) {
    Write-Warning "Gallery Ops Runner not built. Building..."
    msbuild $ScriptRoot\NuGetGalleryOps.sln
    Write-Warning "Gallery Ops Runner has been built, try your command again."
    exit;
}

$tmpfile;
if($CurrentDeployment) {
    # Write a temp file with config data
    $tmpfile = [IO.Path]::GetTempFileName()
}
try {
    if($tmpfile) {
        & $ScriptRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe @args -ConfigFile $tmpfile -EnvironmentName $CurrentEnvironment.Name
    } else {
        & $ScriptRoot\Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe @args
    }
} finally {
    if($tmpfile -and (Test-Path $tmpfile)) {
        del $tmpfile
    }
}