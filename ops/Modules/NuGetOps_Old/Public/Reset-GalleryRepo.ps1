<#
.SYNOPSIS
Resets the specified Gallery Repository

.PARAMETER RepositoryPath
The path to the NuGetGallery repository

.PARAMETER RepositoryURL
The URL to clone the gallery from (optional, defaults to the NuGet/NuGetGallery repo on GitHub)
#>
function Reset-GalleryRepo {
	param(
		[string]$RepositoryPath = $null,
		[string]$RepositoryURL = "https://github.com/NuGet/NuGetGallery.git"
	)
	if([String]::IsNullOrEmpty($RepositoryPath)) {
		$RepositoryPath = Join-Path $OpsRoot "NuGetGallery"
	}
	if(Test-Path $RepositoryPath) {
		Write-Host "Removing old Gallery clone..."
		del $RepositoryPath -Force -Recurse
	}
	Write-Host "Cloning gallery..."
	git clone $RepositoryURL $RepositoryPath
}