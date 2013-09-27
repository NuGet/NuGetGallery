using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class FileConventions
    {
        public static string GetPackageFileName(
            string id,
            string version)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            if (version == null)
            {
                throw new ArgumentNullException("version");
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
