[CmdletBinding()]
param(
    [string]$Branch
)

# This file is downloaded to "build/init.ps1" so use the parent folder as the root
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent

# Download common.ps1 and other tools used by this build script
$RootGitHubApiUri = "https://api.github.com/repos/NuGet/ServerCommon/contents"

if ($Branch) {
    $Ref = '?ref=' + $Branch
} else {
    $Ref = ''
}

Function Download-Folder {
    [CmdletBinding()]
    param(
        [string]$Path
    )
    
    $DirectoryPath = (Join-Path $NuGetClientRoot $FilePath)
    if (-not (Test-Path $DirectoryPath)) {
        New-Item -Path $DirectoryPath -ItemType "directory"
    }
    
    $FolderUri = "$RootGitHubApiUri/$Path$Ref"
    Write-Host "Downloading files from $FolderUri"
    $Files = wget $FolderUri | ConvertFrom-Json
    Foreach ($File in $Files) {
        $FilePath = $File.path
        if ($File.type -eq "file") {
            $DownloadUrl = $File.download_url
            Write-Host "Downloading file at $DownloadUrl"
            wget -Uri $DownloadUrl -OutFile (Join-Path $NuGetClientRoot $FilePath)
        } elseif ($File.type -eq "dir") {
            Download-Folder -Path $FilePath
        }
    }
}

$FoldersToDownload = "build", "tools"
foreach ($Folder in $FoldersToDownload) {
    Download-Folder -Path $Folder
}

# Run common.ps1
. "$NuGetClientRoot\build\common.ps1"