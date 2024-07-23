[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$CommonAssemblyVersion = '3.0.0',
    [string]$CommonPackageVersion = '3.0.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA
)

trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

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

Invoke-BuildStep 'Cleaning test results' { Clear-Tests } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' {
        $CommonAssemblyInfo =
            "src\NuGet.Services.Build\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Configuration\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Contracts\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Cursor\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.FeatureFlags\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Incidents\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.KeyVault\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Licenses\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Logging\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Messaging.Email\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Messaging\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Owin\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.ServiceBus\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Sql\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Status.Table\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Status\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Storage\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Testing.Entities\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Validation.Issues\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Validation\Properties\AssemblyInfo.g.cs"
        $CommonAssemblyInfo | ForEach-Object {
            Set-VersionInfo (Join-Path $PSScriptRoot $_) -AssemblyVersion $CommonAssemblyVersion -PackageVersion $CommonPackageVersion -Branch $Branch -Commit $CommitSHA 
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { `
    Install-SolutionPackages -path (Join-Path $PSScriptRoot "packages.config") -output (Join-Path $PSScriptRoot "packages") -excludeversion } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Removing .editorconfig file in ServerCommon' { Remove-EditorconfigFile -Directory $PSScriptRoot } `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { `
        $SolutionPath = Join-Path $PSScriptRoot "NuGet.Server.Common.sln"
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $SolutionPath -SkipRestore:$SkipRestore
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber `
    } `
    -ev +BuildErrors
    
Invoke-BuildStep 'Creating artifacts' { `
        $CommonProjects =
            "src\NuGet.Services.Build\NuGet.Services.Build.csproj",
            "src\NuGet.Services.Configuration\NuGet.Services.Configuration.csproj",
            "src\NuGet.Services.Contracts\NuGet.Services.Contracts.csproj",
            "src\NuGet.Services.Cursor\NuGet.Services.Cursor.csproj",
            "src\NuGet.Services.FeatureFlags\NuGet.Services.FeatureFlags.csproj",
            "src\NuGet.Services.Incidents\NuGet.Services.Incidents.csproj",
            "src\NuGet.Services.KeyVault\NuGet.Services.KeyVault.csproj",
            "src\NuGet.Services.Licenses\NuGet.Services.Licenses.csproj",
            "src\NuGet.Services.Logging\NuGet.Services.Logging.csproj",
            "src\NuGet.Services.Messaging.Email\NuGet.Services.Messaging.Email.csproj",
            "src\NuGet.Services.Messaging\NuGet.Services.Messaging.csproj",
            "src\NuGet.Services.Owin\NuGet.Services.Owin.csproj",
            "src\NuGet.Services.ServiceBus\NuGet.Services.ServiceBus.csproj",
            "src\NuGet.Services.Sql\NuGet.Services.Sql.csproj",
            "src\NuGet.Services.Status.Table\NuGet.Services.Status.Table.csproj",
            "src\NuGet.Services.Status\NuGet.Services.Status.csproj",
            "src\NuGet.Services.Storage\NuGet.Services.Storage.csproj",
            "src\NuGet.Services.Testing.Entities\NuGet.Services.Testing.Entities.csproj",
            "src\NuGet.Services.Validation.Issues\NuGet.Services.Validation.Issues.csproj",
            "src\NuGet.Services.Validation\NuGet.Services.Validation.csproj"
        $CommonProjects | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $CommonPackageVersion
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
        Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber `
    } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
