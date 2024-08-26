[CmdletBinding()]
param(
    [string]$BuildBranchCommit
)

if ($env:TF_BUILD) {
    Write-Host "##[group]Fetching build tools"
}

Write-Host "Loading build tools version $BuildBranchCommit..." -ForegroundColor Green

# This file is downloaded to "build/init.ps1" so use the parent folder as the root
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$BuildToolsCloneRoot = Join-Path $NuGetClientRoot "build\.clone";

# store boolean in an object so we can pass by reference
$IsBuildToolsCloned = @{ Cloned = $false }

Function Invoke-CloneBuildTools {
    param(
        [string]$BuildBranchCommit
    )

    if ($IsBuildToolsCloned.Cloned) {
        return
    }
    
    Write-Host "Fetching build tools repository (NuGet/NuGetGallery)..." -ForegroundColor Blue
    & cmd /c "git init && git remote add origin https://github.com/NuGet/NuGetGallery.git 2>&1"
    & cmd /c "git fetch origin $BuildBranchCommit 2>&1"
    Write-Host "Build tools repository retrieved on $BuildBranchCommit commit." -ForegroundColor Blue
    $IsBuildToolsCloned.Cloned = $true
}

Function Get-BuildTools {
    param(
        [string]$BuildBranchCommit
    )

    Function Get-Folder {
        [CmdletBinding()]
        param(
            [string]$Path
        )
        # Create directory if not exists in root
        $DirectoryPath = (Join-Path $NuGetClientRoot $Path)
        if (-not (Test-Path $DirectoryPath)) {
            New-Item -Path $DirectoryPath -ItemType "directory" | Out-Null
        }

        # Verifies if marker file on the directory contains latest commit
        $MarkerFile = Join-Path $DirectoryPath ".marker"
        if (Test-Path $MarkerFile) {
            $content = Get-Content $MarkerFile
            if ($content -eq $BuildBranchCommit) {
                Write-Host "Build tools directory '$Path' is already at $BuildBranchCommit" -ForegroundColor Blue
                return
            }
        }

        Invoke-CloneBuildTools $BuildBranchCommit

        # check out the directory
        Write-Host "Copying directory '$Path' from build tools" -ForegroundColor Blue
        & cmd /c "git checkout $BuildBranchCommit -- $Path 2>&1"
        
        # Recursively creates the inner directories
        $FolderUri = Join-Path $BuildToolsCloneRoot $Path
        $InnerDirectories = Get-ChildItem -Path $FolderUri -Directory
        foreach ($InnerDirectory in $InnerDirectories) {
            $InnerDirectoryPath = ($InnerDirectory.FullName).Replace($BuildToolsCloneRoot, "")
            Get-Folder -Path $InnerDirectoryPath
        }

        # Gets all files from current repository directory and moves them to root directory
        $FileDirectory = Join-Path $NuGetClientRoot $Path
        $FilesToMove = Get-ChildItem -Path $FolderUri -File
        foreach ($File in $FilesToMove) {
            $DestinationFile = Join-Path $FileDirectory $File.Name
            
            if (-not (Test-Path $DestinationFile)) {
                Move-Item -Path $File.FullName -Destination $FileDirectory
            }
            else {
                Write-Host "File '$($File.Name)' already exists, skipping" -ForegroundColor Blue
            }
        }

        # Creates the marker file for the current directory
        $BuildBranchCommit | Out-File $MarkerFile
    }

    $FoldersToMove = "build", "tools\7zip"
    foreach ($Folder in $FoldersToMove) {
        Get-Folder -Path $Folder
    }
}

if (-not (Test-Path $BuildToolsCloneRoot)) {
    New-Item -ItemType directory -Path $BuildToolsCloneRoot | Out-Null
}
Set-Location $BuildToolsCloneRoot
Get-BuildTools -BuildBranchCommit $BuildBranchCommit
Set-Location $NuGetClientRoot
Remove-Item -Path $BuildToolsCloneRoot -Recurse -Force

# Run common.ps1
. "$NuGetClientRoot\build\common.ps1"

if ($env:TF_BUILD) {
    Write-Host "##[endgroup]"
}
