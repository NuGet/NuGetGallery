namespace NuGetGallery.GitHub
{
    public interface IGitHubUsageConfiguration
    {
        /// <summary>
        /// Returns a NuGetPackageGitHubInformation object that contains the information about a NuGet package.
        /// NOTE: If a packageId has no information, the NuGetPackageGitHubInformation's TotalRepos will be 0 and the Repos list will be null
        /// 
        /// throws an ArgumentException if the packageId is null
        /// </summary>
        /// <param name="packageId">NuGet package id</param>
        /// <returns></returns>
        NuGetPackageGitHubInformation GetPackageInformation(string packageId);
    }
}
