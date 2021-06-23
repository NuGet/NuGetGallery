[CmdletBinding()]
param(
    [string]$BuildBranchCommit
)

# This file is downloaded to "build/init.ps1" so use the parent folder as the root
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$ServerCommonRoot = Join-Path $NuGetClientRoot "\ServerCommon";

Function Get-BuildTools {
    param(
        [string]$BuildBranchCommit
    )
    Write-Host "Getting ServerCommon repository..."
    & cmd /c "git init && git remote add origin https://github.com/NuGet/ServerCommon.git 2>&1"
    & cmd /c "git fetch origin $BuildBranchCommit 2>&1"
    & cmd /c "git reset --hard FETCH_HEAD 2>&1"
    Write-Host "ServerCommon repository retrieved on $BuildBranchCommit commit."

    Function Get-Folder {
        [CmdletBinding()]
        param(
            [string]$Path
        )
        # Create directory if not exists in root
        $DirectoryPath = (Join-Path $NuGetClientRoot $Path)
        if (-not (Test-Path $DirectoryPath)) {
            New-Item -Path $DirectoryPath -ItemType "directory"
        }

        # Verifies if marker file on the directory contains latest commit
        $MarkerFile = Join-Path $DirectoryPath ".marker"
        if (Test-Path $MarkerFile) {
            $content = Get-Content $MarkerFile
            if ($content -eq $BuildBranchCommit) {
                Write-Host "Build tools directory '$Path' is already at '$BuildBranchCommit'."
                return;
            }
        }
        
        # Recursively creates the inner directories
        $FolderUri = Join-Path $ServerCommonRoot $Path
        $InnerDirectories = Get-ChildItem -Path $FolderUri -Directory
        foreach ($InnerDirectory in $InnerDirectories)
        {
            $InnerDirectoryPath = ($InnerDirectory.FullName).Replace("$ServerCommonRoot", "")
            Get-Folder -Path $InnerDirectoryPath
        }

        # Gets all files from current repository directory and moves them to root directory
        $FileDirectory = Join-Path $NuGetClientRoot $Path
        $FilesToMove = Get-ChildItem -Path $FolderUri -File
        Foreach ($File in $FilesToMove)
        {
            if (-not (Test-Path (Join-Path $FileDirectory $File)))
            {
                $File | Move-Item -Destination $FileDirectory
            }
            else
            {
                Write-Host "File $File Already created"
            }
        }

        # Creates the marker file for the current directory
        $BuildBranchCommit | Out-File $MarkerFile
    }

    $FoldersToMove = "build", "tools"
    foreach ($Folder in $FoldersToMove) {
        Get-Folder -Path $Folder
    }
}

if (-not (Test-Path $ServerCommonRoot))
{
    New-Item -ItemType directory -Path $ServerCommonRoot
}
Set-Location $ServerCommonRoot
Get-BuildTools -BuildBranchCommit $BuildBranchCommit
Set-Location $NuGetClientRoot
Remove-Item -Path $ServerCommonRoot -Recurse -Force

# Run common.ps1
. "$NuGetClientRoot\build\common.ps1"