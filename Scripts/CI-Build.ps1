param($commitSha, $commitBranch)
try {
    $ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
    . $ScriptRoot\_Common.ps1

    #Set up env
    if ((test-path "C:\Scripts\Set-PreviewVars.ps1") -eq $false) {
        Write-Host "Env could not be setup"
    } 
    C:\Scripts\Set-PreviewVars.ps1

    #Do Work Brah
    & "$ScriptRoot\Package.ps1" -commitSha $commitSha -commitBranch $commitBranch
    & "$ScriptRoot\Deploy.ps1" -promptBeforDelete $false -commitSha $commitSha -commitBranch $commitBranch

} catch {
    $error[0]
    exit 1
}
