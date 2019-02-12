[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$SimpleVersion = '1.0.0',
    [string]$SemanticVersion = '1.0.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
    [string]$BuildBranch = 'b5f9d1c89da96c462935e2195ceb00e69287b93e'
)

# For TeamCity - If any issue occurs, this script fails the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

if (-not (Test-Path "$PSScriptRoot/build")) {
    New-Item -Path "$PSScriptRoot/build" -ItemType "directory"
}

# Enable TLS 1.2 since GitHub requires it.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

wget -UseBasicParsing -Uri "https://raw.githubusercontent.com/NuGet/ServerCommon/$BuildBranch/build/init.ps1" -OutFile "$PSScriptRoot/build/init.ps1"
. "$PSScriptRoot/build/init.ps1" -BuildBranch "$BuildBranch"

Function Clean-Tests {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Cleaning test results'
    
    Remove-Item (Join-Path $PSScriptRoot "Results.*.xml")
}

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning test results' { Clean-Tests } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors
    
Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors
    
Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors
    
Invoke-BuildStep 'Restoring solution packages' { `
        Install-SolutionPackages -path (Join-Path $PSScriptRoot ".nuget\packages.config") -output (Join-Path $PSScriptRoot "packages")
    } `
    -skip:$SkipRestore `
    -ev +BuildErrors
    
Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' {
        $assemblyInfos = `
            "src\NuGet.Indexing\Properties\AssemblyInfo.g.cs", `
            "src\Catalog\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.ApplicationInsights.Owin\Properties\AssemblyInfo.g.cs", `
            "src\Ng\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Services.Metadata.Catalog.Monitoring\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Protocol.Catalog\Properties\AssemblyInfo.g.cs"

        Foreach ($assemblyInfo in $assemblyInfos) {
            Set-VersionInfo -Path (Join-Path $PSScriptRoot $assemblyInfo) -Version $SimpleVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
        $SolutionPath = Join-Path $PSScriptRoot "NuGet.Services.Metadata.sln"
        Build-Solution $Configuration $BuildNumber -MSBuildVersion "15" $SolutionPath -SkipRestore:$SkipRestore `
    } `
    -ev +BuildErrors
    
Invoke-BuildStep 'Creating artifacts' {
        $csprojPackages = `
            "src\NuGet.Indexing\NuGet.Indexing.csproj", `
            "src\Catalog\NuGet.Services.Metadata.Catalog.csproj", `
            "src\NuGet.ApplicationInsights.Owin\NuGet.ApplicationInsights.Owin.csproj", `
            "src\NuGet.Services.Metadata.Catalog.Monitoring\NuGet.Services.Metadata.Catalog.Monitoring.csproj"

        $csprojPackages | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -Symbols -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
        }

        $nuspecPackages = `
            "src\Ng\Catalog2Dnx.nuspec", `
            "src\Ng\Catalog2Lucene.nuspec", `
            "src\Ng\Catalog2Monitoring.nuspec", `
            "src\Ng\Catalog2Registration.nuspec", `
            "src\Ng\Feed2Catalog.nuspec", `
            "src\Ng\Monitoring2Monitoring.nuspec", `
            "src\Ng\MonitoringProcessor.nuspec", `
            "src\Ng\Ng.nuspec", `
            "src\Ng\Ng.Operations.nuspec"

        $nuspecPackages | ForEach-Object {
            New-Package (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
        }
    } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)