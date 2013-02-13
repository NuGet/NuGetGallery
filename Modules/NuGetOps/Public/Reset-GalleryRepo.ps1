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