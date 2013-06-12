
namespace NuGetGallery
{
    public static class PackageHelper
    {
        public static string ParseTags(string tags)
        {
            return tags.Replace(',', ' ').Replace("  ", " ");
        }
    }
}