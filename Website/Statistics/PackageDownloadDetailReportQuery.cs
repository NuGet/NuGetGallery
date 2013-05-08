using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Commands;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadDetailReportQuery : Query<Task<PackageDownloadDetailReport>>
    {
        private string id;
        private string version;

        public PackageDownloadDetailReportQuery(string id, string version)
        {
            // TODO: Complete member initialization
            this.id = id;
            this.version = version;
        }

        public override Task<PackageDownloadDetailReport> Execute()
        {
            throw new NotImplementedException();
        }
    }
}
