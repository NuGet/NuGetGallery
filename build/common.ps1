### Constants ###
$DefaultMSBuildVersion = '15'
$DefaultConfiguration = 'debug'
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$Artifacts = Join-Path $NuGetClientRoot artifacts
$MSBuildRoot = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\'
$MSBuildExeRelPath = 'bin\msbuild.exe'
$VisualStudioVersion=14.0

Set-Alias nuget $NuGetExe

$OrigBgColor = $host.ui.rawui.BackgroundColor
$OrigFgColor = $host.ui.rawui.ForegroundColor

### Functions ###
Function Trace-Log($TraceMessage = '') {
    Write-Host "[$(Trace-Time)]`t$TraceMessage" -ForegroundColor Cyan
}

Function Verbose-Log($VerboseMessage) {
    Write-Verbose "[$(Trace-Time)]`t$VerboseMessage"
}

Function Error-Log {
    param(
        [string]$ErrorMessage,
        [switch]$Fatal)
    if (-not $Fatal) {
        Write-Error "[$(Trace-Time)]`t$ErrorMessage"
    }
    else {
        Write-Error "[$(Trace-Time)]`t$ErrorMessage" -ErrorAction Stop
    }
}

Function Warning-Log($WarningMessage) {
    Write-Warning "[$(Trace-Time)]`t$WarningMessage"
}

Function Trace-Time() {
    $currentTime = Get-Date
    $lastTime = $Global:LastTraceTime
    $Global:LastTraceTime = $currentTime
    "{0:HH:mm:ss} +{1:F0}" -f $currentTime, ($currentTime - $lastTime).TotalSeconds
}

$Global:LastTraceTime = Get-Date

Function Format-ElapsedTime($ElapsedTime) {
    '{0:F0}:{1:D2}' -f $ElapsedTime.TotalMinutes, $ElapsedTime.Seconds
}

# MSBUILD has a nasty habit of leaving the foreground color red
Function Reset-Colors {
    $host.ui.rawui.BackgroundColor = $OrigBgColor
    $host.ui.rawui.ForegroundColor = $OrigFgColor
}

Function Clear-Artifacts {
    [CmdletBinding()]
    param()
    if (Test-Path $Artifacts) {
        Trace-Log 'Clearing the Artifacts folder'
        Remove-Item $Artifacts\* -Recurse -Force
    }
	else {
		New-Item $Artifacts -Type Directory
	}
}

Function Get-MSBuildExe {
    param(
        [string]$MSBuildVersion
    )

    $MSBuildExe = Join-Path $MSBuildRoot ($MSBuildVersion + ".0")
    Join-Path $MSBuildExe $MSBuildExeRelPath
}

Function Invoke-BuildStep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True)]
        [string]$BuildStep,
        [Parameter(Mandatory=$True)]
        [ScriptBlock]$Expression,
        [Parameter(Mandatory=$False)]
        [Alias('args')]
        [Object[]]$Arguments,
        [Alias('skip')]
        [switch]$SkipExecution
    )
    if (-not $SkipExecution) {
        if ($env:TEAMCITY_VERSION) {
            Write-Output "##teamcity[blockOpened name='$BuildStep']"
        }

        Trace-Log "[BEGIN] $BuildStep"
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $completed = $false

        try {
            Invoke-Command $Expression -ArgumentList $Arguments -ErrorVariable err
            $completed = $true
        }
        finally {
            $sw.Stop()
            Reset-Colors
            if ($completed) {
                Trace-Log "[DONE +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }
            else {
                if (-not $err) {
                    Trace-Log "[STOPPED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
                }
                else {
                    Error-Log "[FAILED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
                }
            }

            if ($env:TEAMCITY_VERSION) {
                Write-Output "##teamcity[blockClosed name='$BuildStep']"
            }
        }
    }
    else {
        Warning-Log "[SKIP] $BuildStep"
    }
}

Function Build-Solution {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [int]$BuildNumber = (Get-BuildNumber),
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
		[string]$SolutionPath,
		[string]$TargetProfile,
		[string]$Target,
		[string]$MSBuildProperties,
        [switch]$SkipRestore
    )
	
	if (-not $SkipRestore) {
        # Restore packages for NuGet.Tooling solution
        Restore-SolutionPackages -path $SolutionPath -MSBuildVersion $MSBuildVersion
    }

    # Build the solution
    $opts = , $SolutionPath
    $opts += "/p:Configuration=$Configuration;BuildNumber=$(Format-BuildNumber $BuildNumber)"
	
	if ($TargetProfile) {
		$opts += "/p:TargetProfile=$TargetProfile"
	}
	
	if ($Target) {
		$opts += "/t:$Target"
	}
	
    if (-not $VerbosePreference) {
        $opts += '/verbosity:minimal'
    }
	
	if ($MSBuildProperties) {
		$opts += $MSBuildProperties
	}

    $MSBuildExe = Get-MSBuildExe $MSBuildVersion

    Trace-Log "$MSBuildExe $opts"
    & $MSBuildExe $opts
    if (-not $?) {
        Error-Log "Build of ${SolutionPath} failed. Code: $LASTEXITCODE"
    }
}

# Downloads NuGet.exe and VSTS Credential provider if missing
Function Install-NuGet {
    [CmdletBinding()]
    param()		
	$NuGetFolderPath = Split-Path -Path $NuGetExe -Parent
	if (-not (Test-Path $NuGetFolderPath )) {
		Trace-Log 'Creating folder "$($NuGetFolderPath)"'
		New-Item $NuGetFolderPath -Type Directory | Out-Null
	}
	else {
		Trace-Log 'Target folder "$($NuGetFolderPath)" already exists.'
	}
	
	Trace-Log 'Downloading latest prerelease of nuget.exe'
	wget https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe -OutFile $NuGetExe
}

Function Get-BuildNumber() {
    $SemanticVersionDate = '2016-06-22'
    [int](((Get-Date) - (Get-Date $SemanticVersionDate)).TotalMinutes / 5)
}

Function Format-BuildNumber([int]$BuildNumber) {
    '{0:D4}' -f $BuildNumber
}

Function Clear-PackageCache {
    [CmdletBinding()]
    param()
    Trace-Log 'Clearing package cache (except the web cache)'

    & $NuGetExe locals packages-cache -clear -verbosity detailed
    #& nuget locals global-packages -clear -verbosity detailed
    & $NuGetExe locals temp -clear -verbosity detailed
}

Function Install-SolutionPackages {
    [CmdletBinding()]
    param(
        [Alias('path')]
        [string]$SolutionPath,
		[Alias('output')]
		[string]$OutputPath,
		[switch]$NonInteractive = $true,
		[switch]$ExcludeVersion = $false
    )
    $opts = , 'install'
	$InstallLocation = $NuGetClientRoot
    if (-not $SolutionPath) {
        $opts += "${NuGetClientRoot}\.nuget\packages.config", '-SolutionDirectory', $NuGetClientRoot
    }
    else {
        $opts += $SolutionPath
		$InstallLocation = Split-Path -Path $SolutionPath -Parent
    }

    if (-not $VerbosePreference) {
        $opts += '-verbosity', 'quiet'
    }
	
	if ($NonInteractive) {
		$opts += '-NonInteractive'
	}
	
	if ($ExcludeVersion) {
		$opts += '-ExcludeVersion'
	}
	
	if ($OutputPath) {
		$opts += '-OutputDirectory', "${OutputPath}"
	}
	
	Trace-Log "Installing packages @""$InstallLocation"""	
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Install failed @""$InstallLocation"". Code: ${LASTEXITCODE}"
    }
}

Function Restore-SolutionPackages {
    [CmdletBinding()]
    param(
        [Alias('path')]
        [string]$SolutionPath,
        [ValidateSet(4, 12, 14, 15)]
        [int]$MSBuildVersion,
		[string]$BuildNumber
    )
	$InstallLocation = $NuGetClientRoot
    $opts = , 'restore'
    if (-not $SolutionPath) {
        $opts += "${NuGetClientRoot}\.nuget\packages.config", '-SolutionDirectory', $NuGetClientRoot
    }
    else {
        $opts += $SolutionPath
		$InstallLocation = Split-Path -Path $SolutionPath -Parent
    }
    if ($MSBuildVersion) {
        $opts += '-MSBuildVersion', $MSBuildVersion
    }

    if (-not $VerbosePreference) {
        $opts += '-verbosity', 'quiet'
    }

    Trace-Log "Restoring packages @""$InstallLocation"""
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Restore failed @""$InstallLocation"". Code: ${LASTEXITCODE}"
    }
}

Function Get-PackageVersion() {
	[CmdletBinding()]
    param(
		[string]$ReleaseLabel = "zlocal",
		[string]$BuildNumber
    )
	
	if (-not $BuildNumber) {
		$BuildNumber = Get-BuildNumber
	}
	
	[string]"$SemanticVersion-$ReleaseLabel$BuildNumber"
}

Function New-Package {
	[CmdletBinding()]
	param(
		[Alias('target')]
		[string]$TargetFilePath,
		[string]$Configuration,		
		[string]$ReleaseLabel = "zlocal",
		[string]$BuildNumber,
		[switch]$NoPackageAnalysis,
		[string]$Version
	)
	Trace-Log "Creating package from @""$TargetFilePath"""
	$opts = , 'pack'
	$opts += $TargetFilePath
	
	if (-not (Test-Path $Artifacts)) {
		New-Item $Artifacts -Type Directory
	}
	
	$opts += '-OutputDirectory', $Artifacts
	$opts += '-Properties', "Configuration=$Configuration"
	
	if (-not $BuildNumber) {
		$BuildNumber = Get-BuildNumber
	}
	
	if ($Version){
		$PackageVersion = $Version
	}
	else {
		$PackageVersion = Get-PackageVersion $ReleaseLabel $BuildNumber
	}
	
	$opts += '-Version', "$PackageVersion"
	
	if ($NoPackageAnalysis) {
		$opts += '-NoPackageAnalysis'
	}
	
    Trace-Log "$NuGetExe $opts"
	& $NuGetExe $opts
	if (-not $?) {
        Error-Log "Pack failed for @""$TargetFilePath"". Code: ${LASTEXITCODE}"
    }
}

Function Set-VersionInfo {
	[CmdletBinding()]
	param(
		[string]$Path,
		[string]$Version,
		[string]$Branch,
		[string]$Commit
	)
	
	if (-not $Version) {
		throw "No version info provided."
	}
	
	if(!(Test-Path $Path)) {
		throw "AssemblyInfo.cs not found at $Path!"
	}
	
	Trace-Log "Getting version info in @""$Path"""
	
	if (-not $Commit) {
		$Commit = git rev-parse --short HEAD
	}
	else {
		if ($Commit.Length -gt 7) {
			$Commit = $Commit.SubString(0, 7)
		}
	}
	
	if (-not $Branch) {
		$Branch = git rev-parse --abbrev-ref HEAD
	}
	
	$BuildDateUtc = [DateTimeOffset]::UtcNow	
	$SemanticVersion = $Version + "-" + $Branch
		
	Trace-Log ("[assembly: AssemblyVersion(""" + $Version + """)]")
	Trace-Log ("[assembly: AssemblyInformationalVersion(""" + $SemanticVersion + """)]")
	Trace-Log ("[assembly: AssemblyMetadata(""Branch"", """ + $Branch + """)]")
	Trace-Log ("[assembly: AssemblyMetadata(""CommitId"", """ + $Commit + """)]")
	Trace-Log ("[assembly: AssemblyMetadata(""BuildDateUtc"", """ + $BuildDateUtc + """)]")
	
	Add-Content $Path ("`r`n[assembly: AssemblyVersion(""" + $Version + """)]")
	Add-Content $Path ("[assembly: AssemblyInformationalVersion(""" + $SemanticVersion + """)]")
	Add-Content $Path "#if !PORTABLE"
	Add-Content $Path ("[assembly: AssemblyMetadata(""Branch"", """ + $Branch + """)]")
	Add-Content $Path ("[assembly: AssemblyMetadata(""CommitId"", """ + $Commit + """)]")
	Add-Content $Path ("[assembly: AssemblyMetadata(""BuildDateUtc"", """ + $BuildDateUtc + """)]")
	Add-Content $Path "#endif"
}