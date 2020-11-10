[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [string]$SolutionDirectory,
    [switch]$SkipRestore,
    [string]$FxCopDirectory,
    [string]$FxCopProject,
    [string]$FxCopRuleSet,
    [string]$FxCopNoWarn,
    [string]$FxCopOutputDirectory
)

# Enable TLS 1.2 since GitHub requires it.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# To avoid repository dependencies, this script relies on the following assumptions:
# - parent directory contains a single *.sln
# - parent directory contains a build.ps1
# - build.ps1 has standard NuGet build arguments
# - build.ps1 was already executed and sources were already fetched

# Discover the solution
if (-not $SolutionDirectory) {
    $SolutionDirectory = Join-Path $PSScriptRoot '..'
}

$SolutionPath = $(Get-ChildItem -Path "$SolutionDirectory/*.sln")[0]

if (-not (Test-Path $SolutionPath)) {
    throw "Solution $SolutionPath does not exist!"
}

# Sync to the solution's build tools version
. "$PSScriptRoot/common.ps1"

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "RunCodeAnalysis Build #$BuildNumber started at $startTime"

$BuildErrors = @()
    
Invoke-BuildStep 'Running code analysis' { 
        Invoke-FxCop $Configuration $BuildNumber $SolutionPath -SkipRestore:$SkipRestore -FxCopDirectory $FxCopDirectory -FxCopProject $FxCopProject -FxCopRuleSet $FxCopRuleSet -FxCopOutputDirectory $FxCopOutputDirectory -FxCopNoWarn $FxCopNoWarn`
    } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "RunCodeAnalysis Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)