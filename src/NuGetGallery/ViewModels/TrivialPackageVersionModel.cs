namespace NuGetGallery
{
    public class TrivialPackageVersionModel : IPackageVersionModel
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Title { get; set; }
    }
}