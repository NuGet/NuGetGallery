using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Packaging
{
    public static class PackagingConstants
    {
        /// <summary>
        /// The maximum package ID length enforced by the database.
        /// This is not equal to the maximum package length that can be submitted to the gallery.
        /// See <see cref="NuGet.Packaging.PackageIdValidator.MaxPackageIdLength"/> for the maximum length that is enforced by the gallery.
        /// </summary>
        public const int PackageIdDatabaseLength = 128;
    }
}
