[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [ValidateSet("Release","rtm", "rc", "beta", "beta2", "final", "xprivate", "zlocal")]
    [string]$ReleaseLabel = 'zlocal',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$SimpleVersion = '1.0.0',
    [string]$SemanticVersion = '1.0.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA
)

$ScriptPath = Split-Path $MyInvocation.InvocationName

& "$ScriptPath\build.ps1" -Configuration $Configuration -ReleaseLabel $ReleaseLabel -BuildNumber $BuildNumber -SkipRestore:$SkipRestore -CleanCache:$CleanCache -SimpleVersion "$SimpleVersion" -SemanticVersion "$SemanticVersion" -Branch "$Branch" -CommitSHA "$CommitSHA"
& "$ScriptPath\test.ps1" -Configuration $Configuration -BuildNumber $BuildNumber