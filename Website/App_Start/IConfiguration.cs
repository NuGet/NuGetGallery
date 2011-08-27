
namespace NuGetGallery {
    public interface IConfiguration {
        string PackageFileDirectory { get; }
        string GalleryOwnerEmail { get; }
    }
}