[CmdletBinding()]
param(
    [string]$BuildBranch
)

# This file is downloaded to "build/init.ps1" so use the parent folder as the root
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent

Function Get-BuildTools {
    param(
        [string]$Branch
    )

    # Download common.ps1 and other tools used by this build script
    $RootGitHubApiUri = "https://api.github.com/repos/NuGet/ServerCommon/contents"

    if ($Branch) {
        $Ref = '?ref=' + $Branch
    } else {
        $Ref = ''
    }

    Function Get-Folder {
        [CmdletBinding()]
        param(
            [string]$Path
        )
        
        $DirectoryPath = (Join-Path $NuGetClientRoot $Path)
        if (-not (Test-Path $DirectoryPath)) {
            New-Item -Path $DirectoryPath -ItemType "directory"
        }

        $MarkerFile = Join-Path $DirectoryPath ".marker"
        if (Test-Path $MarkerFile) {
            $content = Get-Content $MarkerFile
            if ($content -eq $Branch) {
                Write-Host "Build tools directory '$Path' is already at '$Branch'."
                return;
            }
        }
        
        $FolderUri = "$RootGitHubApiUri/$Path$Ref"
        Write-Host "Downloading files from $FolderUri"
        $Files = wget -UseBasicParsing $FolderUri | ConvertFrom-Json
        Foreach ($File in $Files) {
            $FilePath = $File.path
            if ($File.type -eq "file") {
                $DownloadUrl = $File.download_url
                Write-Host "Downloading file at $DownloadUrl"
                wget -UseBasicParsing -Uri $DownloadUrl -OutFile (Join-Path $NuGetClientRoot $FilePath)
            } elseif ($File.type -eq "dir") {
                Get-Folder -Path $FilePath
            }
        }

        $Branch | Out-File $MarkerFile
    }

    $FoldersToDownload = "build", "tools"
    foreach ($Folder in $FoldersToDownload) {
        Get-Folder -Path $Folder
    }
}

Get-BuildTools -Branch $BuildBranch

# Run common.ps1
. "$NuGetClientRoot\build\common.ps1"