
namespace NuGetGallery
{
    public static class PackageHelper
    {
        public static string ParseTags(string tags)
        {
            if (tags == null)
            {
                return null;
            }
            return tags.Replace(',', ' ').Replace(';', ' ').Replace('\t', ' ').Replace("  ", " ");
        }
    }
}