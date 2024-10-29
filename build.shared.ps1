param (
    [switch]$SkipCommon,
    [string]$CommonAssemblyVersion,
    [string]$CommonPackageVersion,
    [switch]$SkipGallery,
    [string]$GalleryAssemblyVersion,
    [string]$GalleryPackageVersion,
    [switch]$SkipJobs,
    [string]$JobsAssemblyVersion,
    [string]$JobsPackageVersion
)

# These are build steps that can be shared with our internal release builds
# It assumes the common build tools have already been imported (e.g. build/common.ps1)

$CommonSolution = Join-Path $PSScriptRoot "NuGet.Server.Common.sln"
$CommonProjects = Get-SolutionProjects $CommonSolution
$GallerySolution = Join-Path $PSScriptRoot "NuGetGallery.sln"
$GalleryProjects = Get-SolutionProjects $GallerySolution
$GalleryFunctionalTestsSolution = Join-Path $PSScriptRoot "NuGetGallery.FunctionalTests.sln"
$GalleryFunctionalTestsProjects = Get-SolutionProjects $GalleryFunctionalTestsSolution
$JobsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.sln"
$JobsProjects = Get-SolutionProjects $JobsSolution
$JobsFunctionalTestsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.FunctionalTests.sln"
$JobsFunctionalTestsProjects = Get-SolutionProjects $JobsFunctionalTestsSolution

$SharedCommonProjects = New-Object System.Collections.ArrayList
$SharedGalleryProjects = New-Object System.Collections.ArrayList
$SharedJobsProjects = New-Object System.Collections.ArrayList

Invoke-BuildStep 'Analyzing project files' {
        # Projects are shared between the solutions. Find all projects shared between the solutions
        $solutions =
            $CommonProjects,
            $GalleryProjects,
            $GalleryFunctionalTestsProjects,
            $JobsProjects,
            $JobsFunctionalTestsProjects
        $all = @()
        $shared = @()
        foreach ($solutionProjects in $solutions) {
            if ($all.Count -gt 0) {
                $shared += $solutionProjects | Where-Object { ($all | ForEach-Object { $_.RelativePath }) -contains $_.RelativePath }
            }
            $all += $solutionProjects
        }
        $all = $all | Sort-Object -Property RelativePath | Get-Unique -AsString
        Trace-Log "Total projects: $($all.Count)"
        $shared = $shared | Sort-Object -Property RelativePath | Get-Unique -AsString
        Trace-Log "Total shared projects: $($shared.Count)"

        # Split them into gallery, jobs, and common sets based on version property in the .csproj
        # Use of MSBuild variable 'GalleryPackageVersion' marks a gallery package
        # Use of MSBuild variable 'JobsPackageVersion' marks a jobs package
        # All others are common packages
        $unversionedCount = 0
        foreach ($SharedProject in $shared) {
            $versionLine = Get-Content $SharedProject.Path | Where-Object { $_ -like '*<PackageVersion*>*' }
            if ($versionLine -like '*GalleryPackageVersion*') {
                $SharedGalleryProjects.Add($SharedProject.RelativePath) | Out-Null
            } elseif ($versionLine -like '*JobsPackageVersion*') {
                $SharedJobsProjects.Add($SharedProject.RelativePath) | Out-Null
            } elseif ($versionLine -like '*CommonPackageVersion*') {
                $SharedCommonProjects.Add($SharedProject.RelativePath) | Out-Null
            } else {
                Trace-Log "Shared project without a <PackageVersion> set: $($SharedProject.RelativePath)"
                $unversionedCount++
            }
        }

        if ($unversionedCount -gt 0) {
            throw "$($unversionedCount) shared projects have no <PackageVersion> set based on GalleryPackageVersion, JobsPackageVersion, or CommonPackageVersion."
        }

        Trace-Log "Total shared common projects: $($SharedCommonProjects.Count)"
        Trace-Log "Total shared gallery projects: $($SharedGalleryProjects.Count)"
        Trace-Log "Total shared jobs projects: $($SharedJobsProjects.Count)"

        # Validate that only src projects are shared. No need to shared tests since they would run multiple times.
        $sharedNonSrc = $shared | Where-Object { !$_.IsSrc }
        if ($sharedNonSrc.Count -gt 0) {
            $sharedNonSrc | ForEach-Object { Trace-Log "Shared project not in src directory: $($_.RelativePath)" }
            throw "$($sharedNonSrc.Count) projects are shared between solutions but are non in the src directory. Only src projects should be shared."
        }

        # Validate all .csproj files are in a solution
        Trace-Log "Checking for .csproj files not included in any solution..."
        $allCsproj = Get-ChildItem $PSScriptRoot -Recurse -Filter "*.csproj" | ForEach-Object { $_.FullName }
        Trace-Log "Found $($allCsproj.Count) .csproj files"
        $missingCsproj = $allCsproj | Where-Object { ($all | ForEach-Object { $_.Path }) -notcontains $_ }
        if ($missingCsproj.Count -gt 0) {
            $missingCsproj | ForEach-Object { Trace-Log "Project not in any solution file: $_" }
            throw "$($missingCsproj.Count) projects are not in any solution file."
        }
    } `
    -ev +BuildErrors

$WrittenAssemblyInfo = New-Object System.Collections.ArrayList
function Confirm-NoDuplicateAssemblyInfo($Path) {
    if ($WrittenAssemblyInfo -contains $Path) {
        throw "Duplicate AssemblyInfo.g.cs: $Path"
    } else {
        $WrittenAssemblyInfo.Add($Path) | Out-Null
    }
}

Invoke-BuildStep 'Setting common version metadata in AssemblyInfo.cs' {
        $CommonAssemblyInfo = $CommonProjects `
            | Where-Object { !$_.IsTest } `
            | Where-Object { !$SkipCommon -or $SharedCommonProjects -contains $_.RelativePath } `
            | Where-Object { $SharedGalleryProjects -notcontains $_.RelativePath } `
            | Where-Object { $SharedJobsProjects -notcontains $_.RelativePath };
        $CommonAssemblyInfo | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $CommonAssemblyVersion -PackageVersion $CommonPackageVersion -Branch $Branch -Commit $CommitSHA
            Confirm-NoDuplicateAssemblyInfo $Path
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Setting gallery version metadata in AssemblyInfo.cs' {
        $GalleryAssemblyInfo = $GalleryProjects `
            | Where-Object { !$_.IsTest } `
            | Where-Object { $SharedCommonProjects -notcontains $_.RelativePath } `
            | Where-Object { !$SkipGallery -or $SharedGalleryProjects -contains $_.RelativePath } `
            | Where-Object { $SharedJobsProjects -notcontains $_.RelativePath };
        $GalleryAssemblyInfo | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $GalleryAssemblyVersion -PackageVersion $GalleryPackageVersion -Branch $Branch -Commit $CommitSHA
            Confirm-NoDuplicateAssemblyInfo $Path
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Setting job version metadata in AssemblyInfo.cs' {
        $JobsAssemblyInfo = $JobsProjects `
            | Where-Object { !$_.IsTest } `
            | Where-Object { $SharedCommonProjects -notcontains $_.RelativePath } `
            | Where-Object { $SharedGalleryProjects -notcontains $_.RelativePath } `
            | Where-Object { !$SkipJobs -or $SharedJobsProjects -contains $_.RelativePath };
        $JobsAssemblyInfo | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $JobsAssemblyVersion -PackageVersion $JobsPackageVersion -Branch $Branch -Commit $CommitSHA
            Confirm-NoDuplicateAssemblyInfo $Path
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors
