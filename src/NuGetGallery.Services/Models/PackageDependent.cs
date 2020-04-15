using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageDependent
    {
        public String Id { get; set; }
        public int DownloadCount { get; set; }

        public String Description { get; set; }
    }

    public class CreatePackageDependents
    {
        public IReadOnlyCollection<PackageDependent> PackageList {get; set;}
        public int TotalDownloads { get; set; }
    }
   
}
      