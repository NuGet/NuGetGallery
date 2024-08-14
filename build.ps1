[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$SkipArtifacts,
    [switch]$SkipCommon,
    [string]$CommonAssemblyVersion = '5.0.0',
    [string]$CommonPackageVersion = '5.0.0-zlocal',
    [switch]$SkipGallery,
    [string]$GalleryAssemblyVersion = '5.0.0',
    [string]$GalleryPackageVersion = '5.0.0-zlocal',
    [switch]$SkipJobs,
    [string]$JobsAssemblyVersion = '5.0.0',
    [string]$JobsPackageVersion = '5.0.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
    [string]$VerifyMicrosoftPackageVersion = $null
)

Set-StrictMode -Version 1.0

trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

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
        $allCsproj = Get-ChildItem (Join-Path $PSScriptRoot "*.csproj") -Recurse
        $missingCsproj = $allCsproj | Where-Object { ($all | ForEach-Object { $_.Path }) -notcontains $_ }
        if ($missingCsproj.Count -gt 0) {
            $missingCsproj | ForEach-Object { Trace-Log "Project not in any solution file: $_" }
            throw "$($missingCsproj.Count) projects are not in any solution file."
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' {
        $SolutionPath = Join-Path $PSScriptRoot "packages.config"
        $PackagesDir = Join-Path $PSScriptRoot "packages"
        Install-SolutionPackages -path $SolutionPath -output $PackagesDir -ExcludeVersion
    } `
    -skip:$SkipRestore `
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

Invoke-BuildStep 'Building common solution' {
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $CommonSolution -SkipRestore:$SkipRestore
    } `
    -skip:$SkipCommon `
    -ev +BuildErrors

Invoke-BuildStep 'Building gallery solution' { 
        $MvcBuildViews = $Configuration -eq "Release"
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $GallerySolution -SkipRestore:$SkipRestore -MSBuildProperties "/p:MvcBuildViews=$MvcBuildViews" `
    } `
    -skip:$SkipGallery `
    -ev +BuildErrors

Invoke-BuildStep 'Building jobs solution' { 
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $JobsSolution -SkipRestore:$SkipRestore
    } `
    -skip:$SkipJobs `
    -ev +BuildErrors 

Invoke-BuildStep 'Building jobs functional test solution' { 
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $JobsFunctionalTestsSolution -SkipRestore:$SkipRestore
    } `
    -skip:$SkipJobs `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber
    } `
    -skip:$SkipArtifacts `
    -ev +BuildErrors

Invoke-BuildStep 'Creating dependency packages from all solutions' {
        $packageVersions = "/p:CommonPackageVersion=$CommonPackageVersion;GalleryPackageVersion=$GalleryPackageVersion;JobsPackageVersion=$JobsPackageVersion"
    
        $CommonPackages = $CommonProjects | Where-Object { $_.IsSrc } | ForEach-Object { $_.RelativePath }
        $CommonPackages | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -Symbols -Options $packageVersions
        }
    } `
    -skip:($SkipCommon -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Creating job packages from gallery solution' { `
        $GalleryNuspecProjects =
            "src\DatabaseMigrationTools\DatabaseMigration.Gallery.nuspec",
            "src\DatabaseMigrationTools\DatabaseMigration.SupportRequest.nuspec",
            "src\DatabaseMigrationTools\DatabaseMigration.Validation.nuspec",
            "src\GitHubVulnerabilities2Db\GitHubVulnerabilities2Db.nuspec",
            "src\GitHubVulnerabilities2v3\GitHubVulnerabilities2v3.nuspec",
            "src\GalleryTools\Gallery.GalleryTools.nuspec",
            "src\VerifyGitHubVulnerabilities\VerifyGitHubVulnerabilities.nuspec"
        $GalleryNuspecProjects | ForEach-Object {
            New-Package (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $GalleryPackageVersion -Branch $Branch
        }

        if (!$VerifyMicrosoftPackageVersion) { $VerifyMicrosoftPackageVersion = $GalleryPackageVersion }
        New-Package (Join-Path $PSScriptRoot "src\VerifyMicrosoftPackage\VerifyMicrosoftPackage.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $VerifyMicrosoftPackageVersion -Branch $Branch
    } `
    -skip:($SkipGallery -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Creating job packages from jobs solution' {
        $JobsNuspecProjects =
            "src\ArchivePackages\ArchivePackages.nuspec",
            "src\CopyAzureContainer\CopyAzureContainer.nuspec",
            "src\Gallery.CredentialExpiration\Gallery.CredentialExpiration.nuspec",
            "src\Gallery.Maintenance\Gallery.Maintenance.nuspec",
            "src\Ng\Catalog2Dnx.nuspec",
            "src\Ng\Catalog2icon.nuspec",
            "src\Ng\Catalog2Monitoring.nuspec",
            "src\Ng\Db2Catalog.nuspec",
            "src\Ng\Db2Monitoring.nuspec",
            "src\Ng\Monitoring2Monitoring.nuspec",
            "src\Ng\MonitoringProcessor.nuspec",
            "src\Ng\Ng.Operations.nuspec",
            "src\NuGet.Jobs.Auxiliary2AzureSearch\NuGet.Jobs.Auxiliary2AzureSearch.nuspec",
            "src\NuGet.Jobs.Catalog2AzureSearch\NuGet.Jobs.Catalog2AzureSearch.nuspec",
            "src\NuGet.Jobs.Catalog2Registration\NuGet.Jobs.Catalog2Registration.nuspec",
            "src\NuGet.Jobs.Db2AzureSearch\NuGet.Jobs.Db2AzureSearch.nuspec",
            "src\NuGet.Jobs.GitHubIndexer\NuGet.Jobs.GitHubIndexer.nuspec",
            "src\NuGet.Services.Revalidate\NuGet.Services.Revalidate.nuspec",
            "src\NuGet.Services.Validation.Orchestrator\Validation.Orchestrator.nuspec",
            "src\NuGet.Services.Validation.Orchestrator\Validation.SymbolsOrchestrator.nuspec",
            "src\NuGet.SupportRequests.Notifications\NuGet.SupportRequests.Notifications.nuspec",
            "src\PackageLagMonitor\Monitoring.PackageLag.nuspec",
            "src\SplitLargeFiles\SplitLargeFiles.nuspec",
            "src\Stats.AggregateCdnDownloadsInGallery\Stats.AggregateCdnDownloadsInGallery.nuspec",
            "src\Stats.CDNLogsSanitizer\Stats.CDNLogsSanitizer.nuspec",
            "src\Stats.CollectAzureCdnLogs\Stats.CollectAzureCdnLogs.nuspec",
            "src\Stats.CollectAzureChinaCDNLogs\Stats.CollectAzureChinaCDNLogs.nuspec",
            "src\Stats.CreateAzureCdnWarehouseReports\Stats.CreateAzureCdnWarehouseReports.nuspec",
            "src\Stats.ImportAzureCdnStatistics\Stats.ImportAzureCdnStatistics.nuspec",
            "src\Stats.PostProcessReports\Stats.PostProcessReports.nuspec",
            "src\Stats.RollUpDownloadFacts\Stats.RollUpDownloadFacts.nuspec",
            "src\StatusAggregator\StatusAggregator.nuspec",
            "src\Validation.PackageSigning.ProcessSignature\Validation.PackageSigning.ProcessSignature.nuspec",
            "src\Validation.PackageSigning.RevalidateCertificate\Validation.PackageSigning.RevalidateCertificate.nuspec",
            "src\Validation.PackageSigning.ValidateCertificate\Validation.PackageSigning.ValidateCertificate.nuspec",
            "src\Validation.Symbols\Validation.Symbols.Job.nuspec"
        $JobsNuspecProjects | ForEach-Object {
            New-Package (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $JobsPackageVersion -Branch $Branch
        }
    } `
    -skip:($SkipJobs -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
        Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber
    } `
    -skip:$SkipArtifacts `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
