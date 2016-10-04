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

. "$PSScriptRoot\build\common.ps1"

Function Run-BinSkim {
	[CmdletBinding()]
	param()
	
	Trace-Log 'Running BinSkim'
	
	$BinSkimExe = (Join-Path $PSScriptRoot "packages\Microsoft.CodeAnalysis.BinSkim\tools\x64\BinSkim.exe")
	
	& $BinSkimExe analyze --config default --verbose (Join-Path $PSScriptRoot "src\NuGetGallery\bin\NuGetGallery.dll")
	& $BinSkimExe analyze --config default --verbose (Join-Path $PSScriptRoot "src\NuGet.Services.Search.Client\bin\$Configuration\NuGet.Services.Search.Client.dll")
	& $BinSkimExe analyze --config default --verbose (Join-Path $PSScriptRoot "src\NuGetGallery.Core\bin\$Configuration\NuGetGallery.Core.dll")
}

Function Run-Tests {
	[CmdletBinding()]
	param()
	
	Trace-Log 'Running tests'
	
	$xUnitExe = (Join-Path $PSScriptRoot "packages\xunit.runner.console\tools\xunit.console.exe")
	
	& $xUnitExe (Join-Path $PSScriptRoot "tests\NuGetGallery.Core.Facts\bin\$Configuration\NuGetGallery.Core.Facts.dll")
	& $xUnitExe (Join-Path $PSScriptRoot "tests\NuGetGallery.Facts\bin\$Configuration\NuGetGallery.Facts.dll")
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
	
Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors
	
Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { `
	Install-SolutionPackages -path (Join-Path $PSScriptRoot ".nuget\packages.config") -output (Join-Path $PSScriptRoot "packages") -excludeversion } `
    -skip:$SkipRestore `
    -ev +BuildErrors
	
Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' { `
	param($Path, $Version, $Branch, $Commit)
	Set-VersionInfo -Path $Path -Version $Version -Branch $Branch -Commit $Commit `
	} `
	-args (Join-Path $PSScriptRoot "src\NuGetGallery\Properties\AssemblyInfo.cs"), $SimpleVersion, $Branch, $CommitSHA `
	-ev +BuildErrors
	
Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' { `
	param($Path, $Version, $Branch, $Commit)
	Set-VersionInfo -Path $Path -Version $Version -Branch $Branch -Commit $Commit `
	} `
	-args (Join-Path $PSScriptRoot "src\NuGetGallery.Core\Properties\AssemblyInfo.cs"), $SimpleVersion, $Branch, $CommitSHA `
	-ev +BuildErrors
		
Invoke-BuildStep 'Building solution' { 
	param($Configuration, $BuildNumber, $SolutionPath, $SkipRestore)
	Build-Solution $Configuration $BuildNumber -MSBuildVersion "14" $SolutionPath -SkipRestore:$SkipRestore -MSBuildProperties "/p:MvcBuildViews=true" `
	} `
	-args $Configuration, $BuildNumber, (Join-Path $PSScriptRoot "NuGetGallery.sln"), $SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Running BinSkim' { Run-BinSkim } `
	-ev +BuildErrors
	
Invoke-BuildStep 'Running tests' { Run-Tests } `
	-ev +BuildErrors
	
Invoke-BuildStep 'Creating artifacts' {
		New-Package (Join-Path $PSScriptRoot "src\NuGetGallery.Core\NuGetGallery.Core.csproj") -Configuration $Configuration -BuildNumber $BuildNumber -ReleaseLabel $ReleaseLabel -Version $SemanticVersion `
		-ev +BuildErrors
	}

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