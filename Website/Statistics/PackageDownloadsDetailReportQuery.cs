using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Commands;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsDetailReportQuery : Query<Task<PackageDownloadsReport>>
    {
        private string id;
        private string version;

        public PackageDownloadsDetailReportQuery(string id, string version)
        {
            // TODO: Complete member initialization
            this.id = id;
            this.version = version;
        }

        public override Task<PackageDownloadsReport> Execute()
        {
            throw new NotImplementedException();
        }
    }
}
