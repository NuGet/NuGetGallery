
namespace NuGetGallery
{
    public class PackageDependency : IEntity
    {
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        public string Id { get; set; }
        public string VersionSpec { get; set; }
    }
}