using NuGet.Versioning;

namespace NuGetGallery
{
    public static class NuGetVersionNormalizer
    {
        public static string Normalize(string version)
        {
            NuGetVersion parsed;
            if (!NuGetVersion.TryParse(version, out parsed))
            {
                return version;
            }

            return parsed.ToNormalizedString();
        }
    }
}