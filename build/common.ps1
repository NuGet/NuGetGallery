### Constants ###
$DefaultMSBuildVersion = '17'
$DefaultConfiguration = 'debug'
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$CLIRoot = Join-Path $NuGetClientRoot 'cli'
$PrivateRoot = Join-Path $PSScriptRoot "private"
$DotNetExeCommand = Get-Command dotnet.exe -ErrorAction Continue
if ($DotNetExeCommand) {
    # prefer dotnet.exe present in build environment
    $DotNetExe = $DotNetExeCommand.Source
}
else {
    $DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
}
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$7zipExe = Join-Path $NuGetClientRoot 'tools\7zip\7za.exe'
$BuiltInVsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$Artifacts = Join-Path $NuGetClientRoot artifacts
$MSBuildRoot = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\'
$MSBuildExeRelPath = 'bin\msbuild.exe'

$NuGetBuildPackageId = 'NuGet.Services.Build'
$NuGetBuildPackageVersion = '1.0.0'

Set-Alias nuget $NuGetExe
Set-Alias dotnet $DotNetExe

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

Function Clear-Tests {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Cleaning test results'
    
    Remove-Item (Join-Path $PSScriptRoot "..\Results.*.xml")
}

Function Get-LatestVisualStudioRoot {

    if (Test-Path $BuiltInVsWhereExe) {
        $installationPath = & $BuiltInVsWhereExe -latest -prerelease -property installationPath
        $installationVersion = & $BuiltInVsWhereExe -latest -prerelease -property installationVersion
        Verbose-Log "Found Visual Studio at '$installationPath' version '$installationVersion' with '$BuiltInVsWhereExe'"
        # Set the fallback version
        $majorVersion = "$installationVersion".Split('.')[0]
        $script:FallbackVSVersion = "$majorVersion.0"

        return $installationPath
    } 

    Error-Log "Could not find a compatible Visual Studio Version because $BuiltInVsWhereExe does not exist" -Fatal
}

Function Get-MSBuildExe {
    param(
        [ValidateSet("15", "16", "17", $null)]
        [string]$MSBuildVersion
    )

    if(-not $MSBuildVersion){
        $MSBuildVersion = Get-VSMajorVersion
    }

    $CommonToolsVar = "Env:VS${MSBuildVersion}0COMNTOOLS"
    if (Test-Path $CommonToolsVar) {
        $CommonToolsValue = gci $CommonToolsVar | select -expand value -ea Ignore
        $MSBuildRoot = Join-Path $CommonToolsValue '..\..\MSBuild' -Resolve
    } else {
        $VisualStudioRoot = Get-LatestVisualStudioRoot
        if ($VisualStudioRoot -and (Test-Path $VisualStudioRoot)) {
            $MSBuildRoot = Join-Path $VisualStudioRoot 'MSBuild'
        }
    }

    $MSBuildExe = Join-Path $MSBuildRoot 'Current\bin\msbuild.exe'

    if (-not (Test-Path $MSBuildExe)) {
        $MSBuildExe = Join-Path $MSBuildRoot "${MSBuildVersion}.0\bin\msbuild.exe"
    }

    if (Test-Path $MSBuildExe) {
        Verbose-Log "Found MSBuild.exe at `"$MSBuildExe`""
        $MSBuildExe
    } else {
        Error-Log 'Could not find MSBuild.exe' -Fatal
    }
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
        if ($env:TF_BUILD) {
            Write-Output "##[group]$BuildStep"
        }

        Trace-Log "[BEGIN] $BuildStep"
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $completed = $false

        try {
            Invoke-Command $Expression -ArgumentList $Arguments -ErrorVariable err #DevSkim: ignore DS104456. Internal build tool called from our build scripts.
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

            if ($env:TF_BUILD) {
                Write-Output "##[endgroup]"
            }
        }
    }
    else {
        Warning-Log "[SKIP] $BuildStep"
    }
}

Function Sign-Binaries {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [int]$BuildNumber = (Get-BuildNumber),
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [string[]]$ProjectsToSign = $null,
        [switch]$BinLog
    )

    if ($ProjectsToSign -eq $null) {
        $repositoryDir = [IO.Path]::GetDirectoryName($PSScriptRoot)
        $defaultProjectsToSign = Join-Path $repositoryDir "src\**\*.csproj"
        $ProjectsToSign = @($defaultProjectsToSign)
    }

    $projectsToSignProperty = $ProjectsToSign -join ';'

    $ProjectPath = Join-Path $PSScriptRoot "sign-binaries.proj"
    Build-Solution $Configuration $BuildNumber -MSBuildVersion "$MSBuildVersion" $ProjectPath -MSBuildProperties "/p:ProjectsToSign=`"$projectsToSignProperty`"" -BinLog:$BinLog
}

Function Sign-Packages {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [int]$BuildNumber = (Get-BuildNumber),
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [switch]$BinLog
    )

    $ProjectPath = Join-Path $PSScriptRoot "sign-packages.proj"
    Build-Solution $Configuration $BuildNumber -MSBuildVersion "$MSBuildVersion" $ProjectPath -BinLog:$BinLog
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
        [switch]$SkipRestore,
        [switch]$BinLog
    )
    
    if (-not $SkipRestore) {
        # Restore packages for NuGet.Tooling solution
        Restore-SolutionPackages -path $SolutionPath -MSBuildVersion $MSBuildVersion
    }

    # Build the solution
    $opts = , $SolutionPath
    $opts += "/p:Configuration=$Configuration;BuildNumber=$(Format-BuildNumber $BuildNumber)"

    # Build in parallel
    # See https://docs.microsoft.com/en-us/visualstudio/msbuild/building-multiple-projects-in-parallel-with-msbuild?view=vs-2017#-maxcpucount-switch
    $opts += "/m"
    
    if ($TargetProfile) {
        $opts += "/p:TargetProfile=$TargetProfile"
    }
    
    if ($Target) {
        $opts += "/t:$Target"
    }
    
    if ($MSBuildProperties) {
        $opts += $MSBuildProperties
    }

    if ($BinLog) {
        $opts += "/bl"
    }

    $MSBuildExe = Get-MSBuildExe $MSBuildVersion

    Trace-Log "$MSBuildExe $opts"
    & $MSBuildExe $opts
    if (-not $?) {
        Error-Log "Build of ${SolutionPath} failed. Code: $LASTEXITCODE"
    }
}

Function Invoke-FxCop {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [int]$BuildNumber = (Get-BuildNumber),
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [string]$SolutionPath,
        [switch]$SkipRestore,
        [string]$FxCopDirectory,
        [string]$FxCopProject,
        [string]$FxCopRuleSet,
        [string]$FxCopNoWarn,
        [string]$FxCopOutputDirectory,
        [switch]$BinLog
    )
    
    # Ensure cleanup from previous runs
    Get-ChildItem -Recurse "*.CodeAnalysisLog.xml" | Remove-Item
    
    $env:FXCOP_DIRECTORY = ''
    $env:FXCOP_PROJECT = ''
    $env:FXCOP_RULESET = ''
    $env:FXCOP_RULESET_DIRECTORY = ''
    $env:FXCOP_OUTPUT_DIRECTORY = ''
    # Do not clear $env:FXCOP_NOWARN, in case set via VSTS variable (was additive)
    
    # Configure FxCop defaults
    $codeAnalysisProps = Resolve-Path $(Join-Path 'build' 'nuget.codeanalysis.props')
    
    # Configure FxCop overrides
    if ($FxCopDirectory) {
        $env:FXCOP_DIRECTORY = $FxCopDirectory
        Trace-Log "Using FXCOP_DIRECTORY=$env:FXCOP_DIRECTORY"
                
        if ($FxCopProject) {
            $items = Get-ChildItem $(Join-Path $FxCopDirectory $FxCopProject) -Recurse
            
            if ($items.Count -gt 0) {
                $env:FXCOP_PROJECT = $items[0]                
                Trace-Log "Discovered FXCOP_PROJECT=$env:FXCOP_PROJECT"
            }
            else {
                throw "Failed to find $FxCopProject under $FxCopDirectory"
            }
        }
        
        if ($FxCopRuleSet) {
            # To support overrides, look for ruleset in build tools first and then fxcop directory.
            $items = Get-ChildItem $(Join-Path $PSScriptRoot $FxCopRuleSet) -Recurse
            if ($items.Count -eq 0) {
                $items = Get-ChildItem $(Join-Path $FxCopDirectory $FxCopRuleSet) -Recurse
            }
            
            if ($items.Count -gt 0) {
                $env:FXCOP_RULESET = $items[0]
                $env:FXCOP_RULESET_DIRECTORY = $($items[0]).Directory
                Trace-Log "Discovered FXCOP_RULESET=$($items[0])"
            }
            else {
                throw "Failed to find $FxCopRuleSet under $FxCopDirectory"
            }
        }
    }
    
    if ($FxCopNoWarn) {
        $env:FXCOP_NOWARN = $FxCopNoWarn
        Trace-Log "Using FXCOP_NOWARN=$FxCopNoWarn"
    }
    
    # Write FxCop logs to specific output directory
    if ($FxCopOutputDirectory) {
        if (-not (Test-Path $FxCopOutputDirectory)) {
            New-Item $FxCopOutputDirectory -Type Directory
        }
        $env:FXCOP_OUTPUT_DIRECTORY = Resolve-Path $FxCopOutputDirectory
        
        Trace-Log "Using FXCOP_OUTPUT_DIRECTORY=$FxCopOutputDirectory"
    }
    
    # Invoke using the msbuild RunCodeAnalysis target
    $msBuildProps = "/p:CustomBeforeMicrosoftCSharpTargets=$codeAnalysisProps;SignType=none;CodeAnalysisVerbose=true"
    
    Build-Solution $Configuration $BuildNumber -MSBuildVersion "$MSBuildVersion" $SolutionPath -Target "Rebuild;RunCodeAnalysis" -MSBuildProperties $msBuildProps -SkipRestore:$SkipRestore -BinLog:$BinLog
}

Function Invoke-Git {
    [CmdletBinding()]
    Param(
        [string[]] $Arguments
    )

    # We are invoking git through cmd here because otherwise the redirection does not process until after git has completed, leaving errors in the stream.
    Trace-Log "git $Arguments"
    & cmd /c "git $Arguments 2>&1"
}

Function Reset-Submodules {
    Trace-Log 'Resetting submodules'
    $args = 'submodule', 'deinit', '--all', '-f'

    Invoke-Git -Arguments $args
}

Function Update-Submodule {
    [CmdletBinding()]
    Param(
        [string] $Name,
        [string] $Path,
        [string] $Branch,
        [string] $RemoteUrl
    )

    Trace-Log "Configuring submodule $Name ($Path) to use branch $Branch."
    $args = 'config', '-f', "$NuGetClientRoot\.gitmodules", "submodule.$Path.branch", "$Branch"

    Invoke-Git -Arguments $args

    If ($RemoteUrl) {
        Trace-Log "Configuring submodule $Name ($Path) to use URL $RemoteUrl."
        $args = 'config', '-f', "$NuGetClientRoot\.gitmodules", "submodule.$Path.url", "$RemoteUrl"

        Invoke-Git -Arguments $args

        Trace-Log "Synchronizing remote URL configuration for submodule $Name ($Path) to the value specified in $NuGetClientRoot\.gitmodules."
        $args = 'submodule', 'sync', '--', "$Path"

        Invoke-Git -Arguments $args
    }

    Trace-Log "Updating submodule $Name ($Path)."
    $args = 'submodule', 'update', '--init', '--remote', '--', "$Path"

    Invoke-Git -Arguments $args
}

Function Install-NuGet {
    [CmdletBinding()]
    param()

    $NuGetFolderPath = Split-Path -Path $NuGetExe -Parent
    $NuGetInstalledMarker = Join-Path $NuGetFolderPath ".marker.v1"

    if (Test-Path $NuGetInstalledMarker) {
        Trace-Log "nuget.exe is already installed"
        Trace-Log "Marker file exists: $NuGetInstalledMarker"
    } else {
        $progressPreference = 'SilentlyContinue'
        try {
            if (-not (Test-Path $NuGetFolderPath)) {
                New-Item $NuGetFolderPath -Type Directory | Out-Null
            }

            Trace-Log 'Downloading nuget.exe'
            Invoke-WebRequest `
                https://dist.nuget.org/win-x86-commandline/v6.10.1/nuget.exe `
                -UseBasicParsing `
                -OutFile $NuGetExe
            
            # Mark nuget.exe and associated files as installed.
            $NuGetInstalledMarker | Out-File -FilePath $NuGetInstalledMarker
        } catch {
            if (Test-Path $NuGetExe) {
                Remove-Item $NuGetExe -Recurse -Force
            }
            throw;
        } finally {
            $progressPreference = 'Continue'
        }
    }
    if (-not (Test-Path $NuGetExe)) {
        throw "No file exists at the expected nuget.exe path: $NuGetExe"
    }
}

Function Configure-NuGetCredentials {
    [CmdletBinding()]
    param(
        [string] $FeedName,
        [string] $Username,
        [string] $PAT,
        [string] $ConfigFile
    )
    Trace-Log "Configuring credentials for $FeedName"

    if (-not $FeedName) {
        Error-Log "Required argument FeedName was not provided."
    }

    if (-not $Username) {
        Error-Log "Required argument Username was not provided."
    }

    $opts = 'sources', 'update', '-NonInteractive', '-Name', "${FeedName}", '-Username', "${Username}"

    if ($PAT) {
        $opts += '-Password', "${PAT}"
    }

    if (Test-Path $ConfigFile) {
        $opts += '-ConfigFile', "${ConfigFile}"
    }

    Trace-Log ("$NuGetExe $opts" -replace "${PAT}", "***")
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Pack failed for @""$TargetFilePath"". Code: ${LASTEXITCODE}"
    }
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
    Trace-Log 'Clearing package cache'

    & $NuGetExe locals http-cache -clear -verbosity detailed
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
        [switch]$ExcludeVersion = $false,
        [string]$ConfigFile
    )
    $opts = , 'install'
    $InstallLocation = $NuGetClientRoot
    if (-not $SolutionPath) {
        $opts += "${NuGetClientRoot}\packages.config", '-SolutionDirectory', $NuGetClientRoot
    }
    else {
        $opts += $SolutionPath
        $InstallLocation = Split-Path -Path $SolutionPath -Parent
    }
    
    if ($ConfigFile) {
        $opts += '-configfile', $ConfigFile
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
        [int]$MSBuildVersion,
        [string]$BuildNumber,
        [string]$ConfigFile
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
        $opts += '-MSBuildPath', (Split-Path -Path (Get-MSBuildExe $MSBuildVersion) -Parent)
    }
    
    if ($ConfigFile) {
        $opts += '-configfile', $ConfigFile
    }

    Trace-Log "Restoring packages @""$InstallLocation"""
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Restore failed @""$InstallLocation"". Code: ${LASTEXITCODE}"
    }
}

function Get-SolutionProjects($SolutionPath) {
    $paths = dotnet sln $SolutionPath list | Where-Object { $_ -like "*.csproj" }
    if (!$paths) {
        throw "Failed to find .csproj files found in solution $SolutionPath."
    }

    $solutionDir = Split-Path (Resolve-Path $SolutionPath)

    $projects = $paths | ForEach-Object {
        $projectPath = Join-Path $solutionDir $_
        $projectRelativeDir = Split-Path $_
        $projectDir = Join-Path $solutionDir $projectRelativeDir
        $isTestProject = $projectRelativeDir -like "test*";
        return [PSCustomObject]@{
            IsTest = $isTestProject;
            Directory = $projectDir;
            Path = $projectPath;
            RelativePath = $_;
        }
    }

    return $projects | Sort-Object -Property Path
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
        [string]$TargetProfile,
        [string]$Configuration,		
        [string]$ReleaseLabel,
        [string]$BuildNumber,
        [switch]$NoPackageAnalysis,
        [string]$PackageId,
        [string]$Version,
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [switch]$Symbols,
        [string]$Branch,
        [switch]$IncludeReferencedProjects,
        [string[]]$NoWarn = @("NU5100", "NU5110", "NU5111", "NU5128")
    )
    Trace-Log "Creating package from @""$TargetFilePath"""
    $opts = , 'pack'
    $opts += $TargetFilePath
    
    if (-not (Test-Path $Artifacts)) {
        New-Item $Artifacts -Type Directory
    }
    
    $OutputDir = Join-Path $Artifacts $TargetProfile
    if (-not (Test-Path $OutputDir)) {
        New-Item $OutputDir -Type Directory
    }
    $opts += '-OutputDirectory', $OutputDir
    
    $Properties = "Configuration=$Configuration"
    if ($TargetProfile) {
        $Properties += ";TargetProfile=$TargetProfile"
    }
    if ($Branch) {
        $Properties += ";branch=$Branch"
    }
    if ($PackageId) {
        $Properties += ";PackageId=$PackageId"
    }
    if ($NoWarn) {
        $Properties += ";NoWarn=" + ($NoWarn -join ",")
    }
    $opts += '-Properties', $Properties
    
    $opts += '-MSBuildPath', (Split-Path -Path (Get-MSBuildExe $MSBuildVersion) -Parent)
    
    if (-not $BuildNumber) {
        $BuildNumber = Get-BuildNumber
    }
    
    if ($Version){
        $PackageVersion = $Version
    }
    elseif ($ReleaseLabel) {
        $PackageVersion = Get-PackageVersion $ReleaseLabel $BuildNumber
    }
    
    if ($PackageVersion) {
        $opts += '-Version', "$PackageVersion"
    }
    
    if ($NoPackageAnalysis) {
        $opts += '-NoPackageAnalysis'
    }
    
    if ($Symbols) {
        $opts += '-Symbols'
    }
    
    if ($IncludeReferencedProjects) {
        $opts += '-IncludeReferencedProjects'
    }
    
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Pack failed for @""$TargetFilePath"". Code: ${LASTEXITCODE}"
    }
}

Function New-WebAppPackage {
    [CmdletBinding()]
    param(
        [Alias('target')]
        [string]$TargetFilePath,
        [string]$TargetProfile,
        [string]$Configuration,
        [string]$BuildNumber,
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [bool]$PackageAsSingleFile=$true,
        [string]$SignType,
        [switch]$BinLog
    )
    Trace-Log "Creating web app package from @""$TargetFilePath"""
    
    $MSBuildExe = Get-MSBuildExe $MSBuildVersion
    
    $opts = , $TargetFilePath
    $opts += "/t:build"
    
    $opts += "/p:Configuration=$Configuration"
    $opts += "/p:BuildNumber=$(Format-BuildNumber $BuildNumber)"
    $opts += "/p:DeployOnBuild=true"
    $opts += "/p:WebPublishMethod=Package"
    $opts += "/p:PackageAsSingleFile=" + $PackageAsSingleFile.ToString().ToLower()
    $opts += "/p:PackageLocation=$Artifacts"
    $opts += "/p:BatchSign=false"
    if ($SignType) { $opts += "/p:SignType=$SignType" }
    
    if (-not (Test-Path $Artifacts)) {
        New-Item $Artifacts -Type Directory
    }
    
    if ($BinLog) {
        $opts += "/bl"
    }

    Trace-Log "$MsBuildExe $opts"
    & $MsBuildExe $opts
    if (-not $?) {
        Error-Log "Creating web app package failed for @""$TargetFilePath"". Code: ${LASTEXITCODE}"
    }
}

Function New-ProjectPackage {
    [CmdletBinding()]
    param(
        [Alias('target')]
        [string]$TargetFilePath,
        [string]$TargetProfile,
        [string]$Configuration,
        [string]$ReleaseLabel,
        [string]$BuildNumber,
        [switch]$NoPackageAnalysis,
        [string]$PackageId,
        [string]$Version,
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [switch]$Symbols,
        [string]$Branch,
        [switch]$IncludeReferencedProjects,
        [switch]$Sign,
        [switch]$BinLog
    )
    Trace-Log "Creating package from @""$TargetFilePath"""
    
    $MSBuildExe = Get-MSBuildExe $MSBuildVersion
    
    $opts = , $TargetFilePath
    $opts += "/t:pack"
    
    $opts += "/p:Configuration=$Configuration;BuildNumber=$(Format-BuildNumber $BuildNumber)"
    $opts += "/p:PackageOutputPath=$Artifacts"
    $opts += "/p:NoBuild=true"
    
    if (-not $Sign)
    {
        $opts += "/p:SignType=none"
    }
    
    if ($PackageId) {
        $opts += "/p:PackageId=$PackageId"
    }
    
    if ($Version){
        $PackageVersion = $Version
    }
    elseif ($ReleaseLabel) {
        $PackageVersion = Get-PackageVersion $ReleaseLabel $BuildNumber
    }
    
    if ($PackageVersion) {
        $opts += "/p:PackageVersion=$PackageVersion"
    }
    
    if ($TargetProfile) {
        $opts += "/p:TargetProfile=$TargetProfile"
    }
    
    if ($NoPackageAnalysis) {
        $opts += '/p:NoPackageAnalysis=True'
    }
    
    if ($Symbols) {
        $opts += "/p:IncludeSymbols=True"
    }
    
    if ($BinLog) {
        $opts += "/bl"
    }

    if (-not (Test-Path $Artifacts)) {
        New-Item $Artifacts -Type Directory
    }
    
    $OutputDir = Join-Path $Artifacts $TargetProfile
    if (-not (Test-Path $OutputDir)) {
        New-Item $OutputDir -Type Directory
    }
    
    Trace-Log "$MsBuildExe $opts"
    & $MsBuildExe $opts
    if (-not $?) {
        Error-Log "Pack failed for @""$TargetFilePath"". Code: ${LASTEXITCODE}"
    }
}

Function Set-AppSetting($webConfig, [string]$name, [string]$value) {
    $setting = $webConfig.configuration.appSettings.add | where { $_.key -eq $name }
    if($setting) {
        $setting.value = $value
        "Set $name = $value."
    } else {
        "Unknown App Setting: $name."
    }
}

Function Set-VersionInfo {
    [CmdletBinding()]
    param(
        [string]$Path,
        [string]$AssemblyVersion,
        [string]$PackageVersion,
        [string]$Branch,
        [string]$Commit
    )
    
    if (-not $AssemblyVersion) {
        throw "No AssemblyVersion provided."
    }
    
    if (-not $PackageVersion) {
        throw "No PackageVersion provided."
    }

    # make sure the directory exists
    $directory = Split-Path $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
        
    Trace-Log "Setting assembly info in ""$Path"""
    
    if (-not $Commit) {
        $Commit = git rev-parse HEAD
    }
    
    if (-not $Branch) {
        $Branch = git rev-parse --abbrev-ref HEAD
    }

    $Content = @"
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Resources;

[assembly: AssemblyVersion("$AssemblyVersion")]
[assembly: AssemblyInformationalVersion("$PackageVersion")]
#if !PORTABLE
[assembly: AssemblyMetadata("Branch", "$Branch")]
[assembly: AssemblyMetadata("CommitId", "$Commit")]
[assembly: AssemblyMetadata("BuildDateUtc", "$([DateTime]::UtcNow.ToString("O"))")]
#endif
"@

    $Content | Out-File $Path -Encoding utf8 -Force 
}

Function Install-PrivateBuildTools() {
    $repository = $env:PRIVATE_BUILD_TOOLS_REPO
    $commit = $env:PRIVATE_BUILD_TOOLS_COMMIT

    if (-Not $commit) {
        $commit = '9f8cdc5d97905ebc7a6ade342b399678fb79af83' #DevSkim: ignore DS173237. Not a token/secret. It is a git commit hash.
    }

    if (-Not $repository) {
        Trace-Log "No private build tools are configured. Use the 'PRIVATE_BUILD_TOOLS_REPO' and 'PRIVATE_BUILD_TOOLS_COMMIT' environment variables."
        return
    }

    Trace-Log "Getting commit $commit from repository $repository"

    if (-Not (Test-Path $PrivateRoot)) {
        git init $PrivateRoot
        git -C $PrivateRoot remote add origin $repository
    }

    git -C $PrivateRoot fetch *>&1 | Out-Null
    git -C $PrivateRoot reset --hard $commit
}

Function Remove-EditorconfigFile() {
    [CmdletBinding()]
    param(
        [string] $Directory
    )

    $NuGetCodeAnalyzerExtensions = $env:NuGetCodeAnalyzerExtensions
    if (-Not $NuGetCodeAnalyzerExtensions) {
        Trace-Log "No NuGet code analyzers are configured. Use the 'NuGetCodeAnalyzerExtensions' environment variable."
        return
    }

    $editorconfigFilePath = Join-Path $Directory ".editorconfig"
    if (-Not (Test-Path $editorconfigFilePath)) {
        Trace-Log ".editorconfig file at $editorconfigFilePath was not found"
        return
    }

    # Remove .editorconfig because the precedence rule for conflicting severity entries from a ruleset file (SDL) and an EditorConfig is undefined.
    # SDL rulesets should be applied for build pipelines.
    # See https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files#severity-options
    Remove-Item $editorconfigFilePath
    Trace-Log "Removed $editorconfigFilePath"
}

Function Add-PackageSourceMapping(
    [Parameter(Mandatory=$True)][string]$NuGetConfigPath,
    [Parameter(Mandatory=$True)][string]$SourceKey,
    [Parameter(Mandatory=$True)][string[]]$Patterns) {
    if (-not [System.IO.Path]::IsPathRooted($NuGetConfigPath)) {
        $NuGetConfigPath = Join-Path $PWD $NuGetConfigPath
    }

    $config = [xml](Get-Content -Raw $NuGetConfigPath)
    if (-not $config.configuration.packageSourceMapping) {
        Write-Host "No package source mapping section. Not doing anything."
        return
    }

    $packageSourceNode = $config.configuration.packageSourceMapping.packageSource | Where-Object { $_.key -eq $SourceKey }
    if (-not $packageSourceNode) {
        $packageSourceNode = $config.CreateElement("packageSource")
        $packageSourceNode.SetAttribute("key", $SourceKey)
        $config.configuration.packageSourceMapping.AppendChild($packageSourceNode) | Out-Null
    }

    foreach ($pattern in $Patterns)
    {
        $package = $config.CreateElement("package")
        $package.SetAttribute("pattern", $pattern)
        $packageSourceNode.AppendChild($package) | Out-Null
    }
    $config.Save($NuGetConfigPath)
}
