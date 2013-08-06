namespace NuGetGallery
{
    public interface IPackageVersionModel
    {
        string Id { get; }
        string Version { get; set; }
        string Title { get; }
    }

    public class TrivialPackageVersionModel : IPackageVersionModel
    {
        public string Id { get; set;  }
        public string Version { get; set; }
        public string Title { get; set; }
    }
}