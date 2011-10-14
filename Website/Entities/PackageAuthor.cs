
namespace NuGetGallery
{
    public class PackageAuthor : IEntity
    {
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }
        public string Name { get; set; }
    }
}