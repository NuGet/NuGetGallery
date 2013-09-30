using System;
using System.Globalization;
using NuGet;

namespace NuGetGallery
{
    public static class FileConventions
    {
        // The latest version of a packge is saved here as a fallback. When *downloading* the latest version of a package
        // it is preferable to supply a hash code so you actually GET the latest version of the package
        // and not just some old version of the package saved in some HTTP cache or CDN somewhere.
        public static string GetPackageFileName(string id, string version)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            SemanticVersion semVer;
            if (!SemanticVersion.TryParse(version, out semVer))
            {
                throw new ArgumentException("not a valid version according to semver", "version");
            }

            if (!String.Equals(semVer.ToNormalizedString(), version, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("the version string is not a normalized version!", "version");
            }

            // Note: packages should be saved and retrieved in blob storage using the lower case version of their filename because
            // a) package IDs can and did change case over time
            // b) blob storage is case sensitive
            // c) it sucks to hit the database just to look up the right case
            // and remember - version can contain letters too.
            return String.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.nupkg",
                id.ToLowerInvariant(),
                version.ToLowerInvariant());
        }

        // Before editing package metadata, the original version of a package is backed up here.
        // Subsequent edits of the package use THIS file, not an edited one, as their base for applying
        // the edit.
        public static string GetBackupOfOriginalPackageFileName(string id, string version)
        {
            return string.Format(
                "packagehistories/{0}/{0}.{1}.nupkg",
                id.ToLowerInvariant(),
                version.ToLowerInvariant());
        }

        // Every single 'edit' or 'version' of a package is saved with its hash here.
        public static string GetPackageFileNameHash(
            string id,
            string version,
            string hash)
        {
            return string.Format(
                "{0}/{0}.{1}.{2}.nupkg",
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                hash);
        }
    }
}
