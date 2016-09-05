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

# For TeamCity - If any issue occurs, this script fail the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

$CLIRoot=$PSScriptRoot
$env:DOTNET_INSTALL_DIR=$CLIRoot

. "$PSScriptRoot\build\common.ps1"

Function Run-BinSkim {
	[CmdletBinding()]
	param()
	
	Trace-Log 'Running BinSkim'
	
	$BinSkimExe = (Join-Path $PSScriptRoot "packages\Microsoft.CodeAnalysis.BinSkim\tools\x64\BinSkim.exe")
	
	& $BinSkimExe analyze --config default --verbose (Join-Path $PSScriptRoot "src\NuGet.Services.Logging\bin\$Configuration\net452\NuGet.Services.Logging.dll")
	& $BinSkimExe analyze --config default --verbose (Join-Path $PSScriptRoot "src\NuGet.Services.KeyVault\bin\$Configuration\NuGet.Services.KeyVault.dll")
	& $BinSkimExe analyze --config default --verbose (Join-Path $PSScriptRoot "src\NuGet.Services.Configuration\bin\$Configuration\net452\NuGet.Services.Configuration.dll")
}

Function Run-Tests {
	[CmdletBinding()]
	param()
	
	Trace-Log 'Running tests'
	
	$xUnitExe = (Join-Path $PSScriptRoot "packages\xunit.runner.console\tools\xunit.console.exe")
	
	& $xUnitExe (Join-Path $PSScriptRoot "tests\NuGet.Services.KeyVault.Tests\bin\$Configuration\NuGet.Services.KeyVault.Tests.dll")
}
	
Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors
	
Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
    -ev +BuildErrors
	
Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors
	
Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' { `
	param($Version, $Branch, $Commit)
	Set-VersionInfo -Path (Join-Path $PSScriptRoot "src\NuGet.Services.KeyVault\Properties\AssemblyInfo.cs") -Version $Version -Branch $Branch -Commit $Commit
	Set-VersionInfo -Path (Join-Path $PSScriptRoot "src\NuGet.Services.Logging\Properties\AssemblyInfo.cs") -Version $Version -Branch $Branch -Commit $Commit
	Set-VersionInfo -Path (Join-Path $PSScriptRoot "src\NuGet.Services.Configuration\Properties\AssemblyInfo.cs") -Version $Version -Branch $Branch -Commit $Commit
	} `
	-args $SimpleVersion, $Branch, $CommitSHA `
	-ev +BuildErrors
	
Invoke-BuildStep 'Restoring solution packages' { `
	Install-SolutionPackages -path (Join-Path $PSScriptRoot ".nuget\packages.config") -output (Join-Path $PSScriptRoot "packages") -excludeversion } `
    -skip:$SkipRestore `
    -ev +BuildErrors
		
Invoke-BuildStep 'Building solution' { `
	param($Configuration, $BuildNumber, $SolutionPath, $SkipRestore) `
	Build-Solution $Configuration $BuildNumber -MSBuildVersion "14" $SolutionPath -SkipRestore:$SkipRestore `
	} `
	-args $Configuration, $BuildNumber, (Join-Path $PSScriptRoot "NuGet.Server.Common.sln"), $SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Running BinSkim' { Run-BinSkim } `
	-ev +BuildErrors
	
Invoke-BuildStep 'Running tests' { Run-Tests } `
	-ev +BuildErrors
	
Invoke-BuildStep 'Creating artifacts' { `
	param($Configuration, $BuildNumber, $ReleaseLabel, $SemanticVersion, $Artifacts) `
		New-Package (Join-Path $PSScriptRoot "src\NuGet.Services.KeyVault\NuGet.Services.KeyVault.csproj") -Configuration $Configuration -Symbols
				
		& dotnet pack (Join-Path $PSScriptRoot "src\NuGet.Services.Logging") --configuration $Configuration --output "$Artifacts" --no-build --version-suffix "$Branch"		
		& dotnet pack (Join-Path $PSScriptRoot "src\NuGet.Services.Configuration") --configuration $Configuration --output "$Artifacts" --no-build --version-suffix "$Branch"
	} `
	-args $Configuration, $BuildNumber, $ReleaseLabel, $SemanticVersion, $Artifacts `
	-ev +BuildErrors

Invoke-BuildStep 'Patching versions of artifacts' {`
		$NupkgWrenchExe = (Join-Path $PSScriptRoot "packages\NupkgWrench\tools\NupkgWrench.exe")
		
		Trace-Log "Patching versions of NuGet packages to $SemanticVersion"
		
		& $NupkgWrenchExe release "$Artifacts" --new-version $SemanticVersion
		& $NupkgWrenchExe release "$Artifacts\NuGet.Services.Configuration.$SemanticVersion.nupkg" --new-version $SemanticVersion --id "NuGet.Services.KeyVault"
		
		Trace-Log "Done"
	}`
	-ev +BuildErrors
	
Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)