using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Commands;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsByIdReportQuery : Query<Task<PackageDownloadsReport>>
    {
        private string id;

        public PackageDownloadsByIdReportQuery(string id)
        {
            // TODO: Complete member initialization
            this.id = id;
        }
        
        public override Task<PackageDownloadsReport> Execute()
        {
            throw new NotImplementedException();
        }
    }
}
