
namespace NuGetGallery
{
    public interface IConfiguration
    {
        string BaseUrl { get; }
        string PackageFileDirectory { get; }
    }
}