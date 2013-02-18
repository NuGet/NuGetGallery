param(
  [Parameter(Mandatory=$false)][string]$ReleaseSha,
  [Parameter(Mandatory=$false)][string]$ReleaseBranch,
  [Parameter(Mandatory=$false)][string]$AzureSdkPath,
  [Parameter(Mandatory=$false)][string]$Configuration = "Release",
  [Parameter(Mandatory=$false)][switch]$ForEmulator)

$AzureToolsRoot = "$env:ProgramFiles\Microsoft SDKs\Windows Azure\"

# Common functions. If we have more scripts, move it to an _Common.ps1 like NuGetGallery has
function Get-AzureSdkPath {
    param($azureSdkPath)
    if(!$azureSdkPath) {
        (dir "$AzureToolsRoot\.NET SDK" | sort Name -desc | select -first 1).FullName
    } else {
        $azureSdkPath
    }
}

# The script itself

$MyPath = split-path $MyInvocation.MyCommand.Path
$RepositoryRoot = resolve-path (join-path $MyPath "..")
$WorkerPath = Join-Path $RepositoryRoot "Source\NuGetGallery.Operations.Worker"
$OutputFolder = Join-Path $RepositoryRoot "_AzurePackage";
$StagingFolder = Join-Path $RepositoryRoot "_PackageStage";

$BuildOutput = Join-Path $WorkerPath "bin\Release"
if(!(Test-Path "$BuildOutput\NuGetGallery.Operations.Worker.dll")) {
  throw "Worker is not built in $Configuration mode. Please build the solution first"
}

if(Test-Path $OutputFolder) {
  del -Recurse -Force $OutputFolder
}
if(Test-Path $StagingFolder) {
  del -Recurse -Force $StagingFolder
}

mkdir $StagingFolder | out-null
cp $BuildOutput\* $StagingFolder

# Build the name
if(!$ReleaseSha) {
  $ReleaseSha = (& git rev-parse --short HEAD)
} elseif($ReleaseSha.Length -gt 10) {
  $ReleaseSha = $ReleaseSha.Substring(0, 10)
}
if(!$ReleaseBranch) {
  $ReleaseBranch = (& git name-rev --name-only HEAD)
}
$PackageFile = Join-Path $OutputFolder "NuGetOperations_$($ReleaseSha)_$ReleaseBranch.cspkg"

# Package!
$copyOnlyFlag = "";
if($ForEmulator) {
  $copyOnlyFlag = "/copyOnly"
}

mkdir $OutputFolder | out-null

$AzureSdkPath = Get-AzureSdkPath $AzureSdkPath
if(!$AzureSdkPath -or !(Test-Path $AzureSdkPath)) {
  throw "Azure SDK not found. Please specify the path to the Azure SDK in the AzureSdkPath parameter."
}
$RoleName = "NuGetGallery.Operations.Worker"
& "$AzureSdkPath\bin\cspack.exe" $copyOnlyFlag "$MyPath\Worker.csdef" /out:"$PackageFile" /role:"$RoleName;$StagingFolder" /rolePropertiesFile:"$RoleName;$MyPath\NuGetOperations.RoleProperties.txt"

write-host "Azure package and configuration dropped to $OutputFolder."
write-host ""

Exit 0