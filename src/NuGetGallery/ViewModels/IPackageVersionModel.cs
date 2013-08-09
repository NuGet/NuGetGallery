namespace NuGetGallery
{
    public interface IPackageVersionModel
    {
        string Id { get; }
        string Version { get; set; }
        string Title { get; }
    }
}