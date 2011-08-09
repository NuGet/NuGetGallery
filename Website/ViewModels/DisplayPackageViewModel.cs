
namespace NuGetGallery
{
    public class DisplayPackageViewModel
    {
        public DisplayPackageViewModel(
            string id,
            string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; set; }
        public string Version { get; set; }

        public string Authors { get; set; }
        public string Description { get; set; }
    }
}