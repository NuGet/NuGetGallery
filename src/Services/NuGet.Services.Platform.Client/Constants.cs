using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public static class BlobContainerNames
    {
        public static readonly string LegacyPackages = "packages";
        public static readonly string LegacyStats = "stats";

        public static readonly string Backups = "ng-backups";
    }

    public static class MimeTypes
    {
        public static readonly string Json = "application/json";
    }
}
