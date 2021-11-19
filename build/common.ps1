### Constants ###
$DefaultMSBuildVersion = '16'
$DefaultConfiguration = 'debug'
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$CLIRoot = Join-Path $NuGetClientRoot 'cli'
$PrivateRoot = Join-Path $PSScriptRoot "private"
$DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
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
        [ValidateSet("15", "16", $null)]
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
    $CredentialProviderDir = Join-Path $NuGetFolderPath "credprovider"
    $CredentialProviderPath = Join-Path $CredentialProviderDir "plugins\netfx\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe"

    if (Test-Path $NuGetInstalledMarker) {
        Trace-Log "nuget.exe is already installed"
        Trace-Log "Marker file exists: $NuGetInstalledMarker"
    } else {
        $progressPreference = 'SilentlyContinue'
        try {
            if (-not (Test-Path $NuGetFolderPath)) {
                New-Item $NuGetFolderPath -Type Directory | Out-Null
            }
    
            $CredentialProviderBundle = (Join-Path $NuGetClientRoot '.nuget\Microsoft.NuGet.CredentialProvider.zip')
    
            Trace-Log 'Downloading Azure Artifacts Credential Provider'
            Invoke-WebRequest `
                https://github.com/microsoft/artifacts-credprovider/releases/download/0.1.18/Microsoft.NuGet.CredentialProvider.zip `
                -UseBasicParsing `
                -OutFile $CredentialProviderBundle
    
            if (Test-Path $CredentialProviderDir) {
                Remove-Item $CredentialProviderDir -Recurse -Force
            }
        
            Trace-Log 'Extracting Azure Artifacts Credential Provider'
            & $7zipExe x $CredentialProviderBundle "-o$CredentialProviderDir" "-y"
            if ($LASTEXITCODE -ne 0) {
                throw "7-zip failed to extract $CredentialProviderBundle"
            }
    
            Remove-Item $CredentialProviderBundle

            Trace-Log 'Downloading nuget.exe'
            Invoke-WebRequest `
                https://dist.nuget.org/win-x86-commandline/v6.0.0-preview4/nuget.exe `
                -UseBasicParsing `
                -OutFile $NuGetExe
            
            # Mark nuget.exe and associated files as installed.
            $NuGetInstalledMarker | Out-File -FilePath $NuGetInstalledMarker
        } catch {
            if (Test-Path $NuGetExe) {
                Remove-Item $NuGetExe -Recurse -Force
            }
            if (Test-Path $CredentialProviderDir) {
                Remove-Item $CredentialProviderDir -Recurse -Force
            }
            if (Test-Path $NuGetInstalledMarker) {
                Remove-Item $NuGetInstalledMarker -Recurse -Force
            }
            throw;
        } finally {
            $progressPreference = 'Continue'
        }
    }

    if (-not (Test-Path $CredentialProviderPath)) {
        throw "No file exists at the expected credential provider path: $CredentialProviderPath"
    }

    if (-not (Test-Path $NuGetExe)) {
        throw "No file exists at the expected nuget.exe path: $NuGetExe"
    }

    Trace-Log "Setting NuGet .NET Framework credential path"
    $env:NUGET_NETFX_PLUGIN_PATHS = $CredentialProviderPath
    $env:NUGET_PLUGIN_PATHS = $CredentialProviderPath
    
    # This is a mitigation for rampant timeouts from the credential provider.
    # See https://github.com/NuGet/Home/issues/7842#issuecomment-531059076
    $timeoutInSeconds = 30
    Trace-Log "Increasing NuGet plug-in timeout values to $timeoutInSeconds seconds."
    $env:NUGET_PLUGIN_HANDSHAKE_TIMEOUT_IN_SECONDS = $timeoutInSeconds
    $env:NUGET_PLUGIN_REQUEST_TIMEOUT_IN_SECONDS = $timeoutInSeconds
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

Function Install-DotnetCLI {
    [CmdletBinding()]
    param()

    Trace-Log 'Downloading Dotnet CLI'

    New-Item -ItemType Directory -Force -Path $CLIRoot | Out-Null

    $env:DOTNET_HOME=$CLIRoot
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    $installDotnet = Join-Path $CLIRoot "dotnet-install.ps1"

    wget -UseBasicParsing 'https://raw.githubusercontent.com/dotnet/cli/release/2.2.1xx/scripts/obtain/dotnet-install.ps1' -OutFile $installDotnet

    & $installDotnet -Channel preview -i $CLIRoot -Version 2.2.100-preview3-009430

    if (-not (Test-Path $DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
    }

    # Delete a project template that is unused and is causing a Component Governance issue.
    # See https://github.com/dotnet/aspnetcore/issues/20001
    $templatesToRemove = Get-ChildItem (Join-Path $CLIRoot "sdk\**\Templates\microsoft.dotnet.web.projecttemplate*")
    Trace-Log "Removing items: "
    $templatesToRemove
    $templatesToRemove | Remove-Item -Force -Recurse

    # Display build info
    & $DotNetExe --info
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
        $opts += "${NuGetClientRoot}\.nuget\packages.config", '-SolutionDirectory', $NuGetClientRoot
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
        [string]$Version,
        [string]$Branch,
        [string]$Commit,
        [string]$AssemblyVersion = $null
    )
    
    if (-not $Version) {
        throw "No version info provided."
    }
    
    if (Test-Path $Path) {
        Remove-Item $Path
    }
    New-Item $Path -ItemType File
    
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
    if (!$AssemblyVersion) {
        $AssemblyVersion = $Version
    }
    $SemanticVersion = $Version + "-" + $Branch
        
    Trace-Log ("[assembly: AssemblyVersion(""" + $AssemblyVersion + """)]")
    Trace-Log ("[assembly: AssemblyInformationalVersion(""" + $SemanticVersion + """)]")
    Trace-Log ("[assembly: AssemblyMetadata(""Branch"", """ + $Branch + """)]")
    Trace-Log ("[assembly: AssemblyMetadata(""CommitId"", """ + $Commit + """)]")
    Trace-Log ("[assembly: AssemblyMetadata(""BuildDateUtc"", """ + $BuildDateUtc + """)]")
    
    Add-Content $Path ("// Copyright (c) .NET Foundation. All rights reserved.")
    Add-Content $Path ("// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.")
    
    Add-Content $Path ("`r`nusing System;")
    Add-Content $Path ("using System.Reflection;")
    Add-Content $Path ("using System.Resources;")
    Add-Content $Path ("using System.Runtime.CompilerServices;")
    Add-Content $Path ("using System.Runtime.InteropServices;")
    
    Add-Content $Path ("`r`n[assembly: AssemblyVersion(""" + $AssemblyVersion + """)]")
    Add-Content $Path ("[assembly: AssemblyInformationalVersion(""" + $SemanticVersion + """)]")
    Add-Content $Path "#if !PORTABLE"
    Add-Content $Path ("[assembly: AssemblyMetadata(""Branch"", """ + $Branch + """)]")
    Add-Content $Path ("[assembly: AssemblyMetadata(""CommitId"", """ + $Commit + """)]")
    Add-Content $Path ("[assembly: AssemblyMetadata(""BuildDateUtc"", """ + $BuildDateUtc + """)]")
    Add-Content $Path "#endif"
}

Function Install-PrivateBuildTools() {
    $repository = $env:PRIVATE_BUILD_TOOLS_REPO
    $commit = $env:PRIVATE_BUILD_TOOLS_COMMIT

    if (-Not $commit) {
        $commit = '4b7460b2e08249e4c65307e5383dfba7fe4da8b7'
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