namespace NuGetGallery {
    public interface IPackageVersionModel {
        string Id { get; set; }
        string Version { get; set; }
        string Title { get; set; }
    }
}