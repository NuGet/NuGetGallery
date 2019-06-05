namespace NuGetGallery.GitHub
{
    public interface IGitHubUsageConfiguration
    {
        NuGetPackageGitHubInformation GetPackageInformation(string packageId);
    }
}
