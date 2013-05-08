using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Commands;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsByVersionReportQuery : Query<Task<PackageDownloadsReport>>
    {
        public override Task<PackageDownloadsReport> Execute()
        {
            throw new NotImplementedException();
        }
    }
}
